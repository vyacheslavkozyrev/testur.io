using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Testurio.Core.Interfaces;
using Testurio.Core.Models;
using Testurio.Core.Repositories;

namespace Testurio.Api.Webhooks;

/// <summary>
/// Minimal ADO comment-created payload. ADO posts a <c>workitem.commented</c> event
/// containing the work-item ID and the comment body.
/// </summary>
internal sealed class AdoCommentPayload
{
    [JsonPropertyName("eventType")]
    public string EventType { get; init; } = string.Empty;

    [JsonPropertyName("resource")]
    public AdoCommentResource? Resource { get; init; }
}

internal sealed class AdoCommentResource
{
    [JsonPropertyName("id")]
    public string CommentId { get; init; } = string.Empty;

    [JsonPropertyName("workItemId")]
    public int WorkItemId { get; init; }

    [JsonPropertyName("text")]
    public string Text { get; init; } = string.Empty;
}

/// <summary>
/// Handles <c>POST /webhooks/ado/{projectId}/comments</c>.
/// Validates the HMAC-SHA256 signature (same mechanism as the status-change webhook),
/// maps the ADO <c>workitem.commented</c> payload to a <see cref="CommentWebhookEvent"/>,
/// and publishes it to the <c>testurio-comment-events</c> Service Bus topic via
/// <see cref="ICommentEventSender"/> (feature 0031, AC-024 / AC-029).
/// </summary>
public static partial class AdoCommentsWebhookHandler
{
    internal const string RoutePrefix = "/v1/webhooks/ado";

    public static IEndpointRouteBuilder MapAdoCommentsWebhook(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup(RoutePrefix).AllowAnonymous();

        group.MapPost("/{projectId}/comments", HandleAsync)
            .WithName("AdoCommentsWebhook");

        return app;
    }

    private static async Task<IResult> HandleAsync(
        string projectId,
        AdoCommentPayload payload,
        IProjectRepository projectRepository,
        ISecretResolver secretResolver,
        ICommentEventSender commentEventSender,
        ILoggerFactory loggerFactory,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger(typeof(AdoCommentsWebhookHandler).FullName!);

        // AC-029: validate HMAC signature before any processing.
        if (!context.Request.Headers.TryGetValue("X-Hub-Signature", out var signatureHeader) ||
            string.IsNullOrWhiteSpace(signatureHeader))
        {
            LogMissingSignature(logger);
            return TypedResults.Unauthorized();
        }

        if (!Guid.TryParse(projectId, out var projectGuid))
            return TypedResults.BadRequest();

        var project = await projectRepository.GetByProjectIdAsync(projectId, cancellationToken);
        if (project is null)
            return TypedResults.Unauthorized();

        // Reuse the Jira-compatible HMAC validation: read body, verify sha256.
        if (!context.Request.Body.CanSeek)
            return TypedResults.Problem("An internal configuration error occurred.", statusCode: 500);

        context.Request.Body.Position = 0;
        using var reader = new StreamReader(context.Request.Body, Encoding.UTF8, leaveOpen: true);
        var body = await reader.ReadToEndAsync(cancellationToken);
        context.Request.Body.Position = 0;

        var secretUri = project.WebhookSecretUri;
        if (string.IsNullOrEmpty(secretUri))
        {
            LogMissingSecret(logger, projectId);
            return TypedResults.Unauthorized();
        }

        var secret = await secretResolver.ResolveAsync(secretUri, cancellationToken);
        var isValid = signatureHeader.Any(v => IsValidSignature(body, v!.Trim(), secret));
        if (!isValid)
        {
            LogInvalidSignature(logger, projectId);
            return TypedResults.Unauthorized();
        }

        // AC-024: accept any comment event regardless of whether it carries the flag —
        // flag detection happens in the worker's FeedbackLoop stage.
        if (payload.EventType != "workitem.commented" || payload.Resource is null)
            return TypedResults.Ok();

        var evt = new CommentWebhookEvent
        {
            PmTool = "ado",
            WorkItemId = payload.Resource.WorkItemId.ToString(),
            CommentBody = payload.Resource.Text,
            CommentId = payload.Resource.CommentId,
            ProjectId = projectGuid,
        };

        await commentEventSender.SendAsync(evt, cancellationToken);
        LogEventPublished(logger, projectId, evt.WorkItemId);

        return TypedResults.Accepted((string?)null);
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

    [LoggerMessage(EventId = 3001, Level = LogLevel.Warning, Message = "ADO comment webhook received without X-Hub-Signature header")]
    private static partial void LogMissingSignature(ILogger logger);

    [LoggerMessage(EventId = 3002, Level = LogLevel.Warning, Message = "ADO comment webhook: no webhook secret configured for project {ProjectId}")]
    private static partial void LogMissingSecret(ILogger logger, string projectId);

    [LoggerMessage(EventId = 3003, Level = LogLevel.Warning, Message = "ADO comment webhook HMAC validation failed for project {ProjectId}")]
    private static partial void LogInvalidSignature(ILogger logger, string projectId);

    [LoggerMessage(EventId = 3004, Level = LogLevel.Information,
        Message = "ADO comment webhook event published for project {ProjectId} / work item {WorkItemId}")]
    private static partial void LogEventPublished(ILogger logger, string projectId, string workItemId);
}
