using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Testurio.Core.Interfaces;
using Testurio.Core.Repositories;

namespace Testurio.Api.Middleware;

public sealed partial class JiraWebhookSignatureFilter : IEndpointFilter
{
    private readonly IProjectRepository _projectRepository;
    private readonly ISecretResolver _secretResolver;
    private readonly ILogger<JiraWebhookSignatureFilter> _logger;

    public JiraWebhookSignatureFilter(
        IProjectRepository projectRepository,
        ISecretResolver secretResolver,
        ILogger<JiraWebhookSignatureFilter> logger)
    {
        _projectRepository = projectRepository;
        _secretResolver = secretResolver;
        _logger = logger;
    }

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var httpContext = context.HttpContext;

        if (!httpContext.Request.Headers.TryGetValue("X-Hub-Signature-256", out var signatureHeader) || string.IsNullOrWhiteSpace(signatureHeader))
        {
            LogMissingSignature(_logger);
            return TypedResults.Unauthorized();
        }

        var projectId = httpContext.GetRouteValue("projectId") as string ?? string.Empty;
        var project = await _projectRepository.GetByProjectIdAsync(projectId, httpContext.RequestAborted);
        if (project is null)
            return TypedResults.Unauthorized();

        if (!httpContext.Request.Body.CanSeek)
            throw new InvalidOperationException("Request body buffering is not configured for the webhook endpoint.");
        httpContext.Request.Body.Position = 0;
        using var reader = new StreamReader(httpContext.Request.Body, Encoding.UTF8, leaveOpen: true);
        var body = await reader.ReadToEndAsync(httpContext.RequestAborted);
        httpContext.Request.Body.Position = 0;

        var secret = await _secretResolver.ResolveAsync(project.JiraWebhookSecretRef, httpContext.RequestAborted);
        if (!IsValidSignature(body, signatureHeader.ToString().Trim(), secret))
        {
            LogInvalidSignature(_logger, projectId);
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
