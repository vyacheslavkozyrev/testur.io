using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Testurio.Api.Middleware;

public class JiraWebhookSignatureMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<JiraWebhookSignatureMiddleware> _logger;

    public JiraWebhookSignatureMiddleware(RequestDelegate next, ILogger<JiraWebhookSignatureMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments("/webhooks/jira"))
        {
            await _next(context);
            return;
        }

        if (!context.Request.Headers.TryGetValue("X-Hub-Signature-256", out var signatureHeader))
        {
            LogMissingSignature(_logger);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        context.Request.EnableBuffering();
        var body = await new StreamReader(context.Request.Body, Encoding.UTF8, leaveOpen: true).ReadToEndAsync();
        context.Request.Body.Position = 0;

        var secret = context.Items["JiraWebhookSecret"] as string;
        if (string.IsNullOrEmpty(secret) || !IsValidSignature(body, signatureHeader!, secret))
        {
            LogInvalidSignature(_logger, context.Request.Path);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        await _next(context);
    }

    private static bool IsValidSignature(string body, string signatureHeader, string secret)
    {
        var secretBytes = Encoding.UTF8.GetBytes(secret);
        var bodyBytes = Encoding.UTF8.GetBytes(body);
        var expectedHash = HMACSHA256.HashData(secretBytes, bodyBytes);
        var expectedSignature = $"sha256={Convert.ToHexString(expectedHash).ToLowerInvariant()}";
        return CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(expectedSignature),
            Encoding.ASCII.GetBytes(signatureHeader.ToString()));
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Jira webhook received without signature header")]
    private static partial void LogMissingSignature(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Jira webhook signature validation failed for {Path}")]
    private static partial void LogInvalidSignature(ILogger logger, PathString path);
}
