using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Testurio.Api.Middleware;
using Testurio.Core.Interfaces;
using Testurio.Core.Models;
using Testurio.Core.Repositories;

namespace Testurio.Api.Webhooks;

/// <summary>
/// Minimal Jira comment-created payload. Jira posts a <c>comment_created</c> event
/// containing the issue key and the comment body.
/// </summary>
internal sealed class JiraCommentCreatedPayload
{
    [JsonPropertyName("webhookEvent")]
    public string WebhookEvent { get; init; } = string.Empty;

    [JsonPropertyName("issue")]
    public JiraCommentIssue? Issue { get; init; }

    [JsonPropertyName("comment")]
    public JiraCommentBody? Comment { get; init; }
}

internal sealed class JiraCommentIssue
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("key")]
    public string Key { get; init; } = string.Empty;
}

internal sealed class JiraCommentBody
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("body")]
    public string Body { get; init; } = string.Empty;
}

/// <summary>
/// Handles <c>POST /v1/webhooks/jira/{projectId}/comments</c>.
/// Validates the Jira shared secret (same HMAC mechanism as the Jira status-change webhook),
/// maps the <c>comment_created</c> payload to a <see cref="CommentWebhookEvent"/>,
/// and publishes it to the <c>testurio-comment-events</c> Service Bus topic via
/// <see cref="ICommentEventSender"/> (feature 0031, AC-024 / AC-030).
/// </summary>
public static partial class JiraCommentsWebhookHandler
{
    public static IEndpointRouteBuilder MapJiraCommentsWebhook(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup(WebhookRouteConstants.JiraPrefix).AllowAnonymous();

        group.MapPost("/{projectId}/comments", HandleAsync)
            .AddEndpointFilter<JiraWebhookSignatureFilter>()
            .WithName("JiraCommentsWebhook");

        return app;
    }

    private static async Task<IResult> HandleAsync(
        string projectId,
        JiraCommentCreatedPayload payload,
        ICommentEventSender commentEventSender,
        ILoggerFactory loggerFactory,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger(typeof(JiraCommentsWebhookHandler).FullName!);

        if (!Guid.TryParse(projectId, out var projectGuid))
            return TypedResults.BadRequest();

        // AC-024: accept any comment event regardless of flag — detection happens in the worker.
        if (payload.WebhookEvent != "comment_created" || payload.Issue is null || payload.Comment is null)
            return TypedResults.Ok();

        var evt = new CommentWebhookEvent
        {
            PmTool = "jira",
            WorkItemId = payload.Issue.Key,
            CommentBody = payload.Comment.Body,
            CommentId = payload.Comment.Id,
            ProjectId = projectGuid,
        };

        await commentEventSender.SendAsync(evt, cancellationToken);
        LogEventPublished(logger, projectId, evt.WorkItemId);

        return TypedResults.Accepted((string?)null);
    }

    [LoggerMessage(EventId = 3005, Level = LogLevel.Information,
        Message = "Jira comment webhook event published for project {ProjectId} / issue {IssueKey}")]
    private static partial void LogEventPublished(ILogger logger, string projectId, string issueKey);
}
