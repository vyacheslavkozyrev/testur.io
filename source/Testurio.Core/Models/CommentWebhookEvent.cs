namespace Testurio.Core.Models;

/// <summary>
/// Represents a comment-created webhook event forwarded from <c>Testurio.Api</c> to the
/// <c>testurio-comment-events</c> Service Bus topic (feature 0031).
/// Consumed by <c>CommentEventJobProcessor</c> in <c>Testurio.Worker</c>, which dispatches to
/// <c>IFeedbackLoop.ProcessAsync</c>.
/// </summary>
public sealed record CommentWebhookEvent
{
    /// <summary>PM tool that originated the event: <c>"ado"</c> or <c>"jira"</c>.</summary>
    public required string PmTool { get; init; }

    /// <summary>Identifier of the work item / issue on which the comment was posted.</summary>
    public required string WorkItemId { get; init; }

    /// <summary>Full text of the posted comment, including any <c>@testurio memorize</c> flag.</summary>
    public required string CommentBody { get; init; }

    /// <summary>PM-tool-assigned comment identifier (used as the upsert key alongside <c>WorkItemId</c>).</summary>
    public required string CommentId { get; init; }

    /// <summary>Testurio project identifier — partition key used to scope Cosmos DB queries.</summary>
    public required Guid ProjectId { get; init; }
}
