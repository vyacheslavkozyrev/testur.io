using Testurio.Core.Models;

namespace Testurio.Core.Interfaces;

/// <summary>
/// Contract for the FeedbackLoop pipeline stage (stage 7, feature 0031).
/// Processes incoming comment-created webhook events, detects the <c>@testurio memorize</c> flag,
/// embeds the feedback text, and upserts a <see cref="TestMemoryEntry"/>-compatible document
/// tagged <c>source: qalead</c> to the <c>TestMemory</c> Cosmos DB container.
/// </summary>
public interface IFeedbackLoop
{
    /// <summary>
    /// Processes a comment-created webhook event.
    /// Returns immediately when the <c>@testurio memorize</c> flag is absent.
    /// Throws on embedding or Cosmos write failure — the Service Bus message must not be settled
    /// when this method throws, so the queue retries per the dead-letter threshold.
    /// </summary>
    /// <param name="evt">The deserialized comment webhook event from the Service Bus message.</param>
    /// <param name="ct">Cancellation token forwarded to every I/O call.</param>
    Task ProcessAsync(CommentWebhookEvent evt, CancellationToken ct);
}
