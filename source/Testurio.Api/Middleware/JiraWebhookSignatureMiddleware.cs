using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Testurio.Core.Repositories;

namespace Testurio.Api.Middleware;

public static partial class JiraWebhookSignatureFilter
{
    public static async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var httpContext = context.HttpContext;
        var logger = httpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("JiraWebhookSignatureFilter");

        if (!httpContext.Request.Headers.TryGetValue("X-Hub-Signature-256", out var signatureHeader) || string.IsNullOrWhiteSpace(signatureHeader))
        {
            LogMissingSignature(logger);
            return TypedResults.Unauthorized();
        }

        var projectId = httpContext.GetRouteValue("projectId") as string ?? string.Empty;
        var projectRepo = httpContext.RequestServices.GetRequiredService<IProjectRepository>();
        var project = await projectRepo.GetByProjectIdAsync(projectId, httpContext.RequestAborted);
        if (project is null)
            return TypedResults.NotFound();

        httpContext.Request.Body.Position = 0;
        var body = await new StreamReader(httpContext.Request.Body, Encoding.UTF8, leaveOpen: true).ReadToEndAsync(httpContext.RequestAborted);
        httpContext.Request.Body.Position = 0;

        if (!IsValidSignature(body, signatureHeader.ToString().Trim(), project.JiraWebhookSecretRef))
        {
            LogInvalidSignature(logger, projectId);
            return TypedResults.Unauthorized();
        }

        httpContext.Items["Project"] = project;
        return await next(context);
    }

    private static bool IsValidSignature(string body, string signatureHeader, string secret)
    {
        var secretBytes = Encoding.UTF8.GetBytes(secret);
        var bodyBytes = Encoding.UTF8.GetBytes(body);
        var expectedHash = HMACSHA256.HashData(secretBytes, bodyBytes);
        var expectedSignature = $"sha256={Convert.ToHexString(expectedHash).ToLowerInvariant()}";
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expectedSignature),
            Encoding.UTF8.GetBytes(signatureHeader));
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Jira webhook received without X-Hub-Signature-256 header")]
    private static partial void LogMissingSignature(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Jira webhook HMAC validation failed for project {ProjectId}")]
    private static partial void LogInvalidSignature(ILogger logger, string projectId);
}
