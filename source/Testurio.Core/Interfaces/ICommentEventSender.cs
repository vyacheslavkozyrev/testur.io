using Testurio.Core.Models;

namespace Testurio.Core.Interfaces;

/// <summary>
/// Publishes <see cref="CommentWebhookEvent"/> messages to the <c>testurio-comment-events</c>
/// Service Bus topic (feature 0031, AC-024).
/// Implemented in <c>Testurio.Infrastructure</c> and injected into the webhook handlers in
/// <c>Testurio.Api</c>.
/// </summary>
public interface ICommentEventSender
{
    /// <summary>
    /// Serialises <paramref name="evt"/> and sends it to the comment-events Service Bus topic.
    /// </summary>
    /// <param name="evt">The comment event to publish.</param>
    /// <param name="cancellationToken">Cancellation token forwarded to the Service Bus SDK call.</param>
    Task SendAsync(CommentWebhookEvent evt, CancellationToken cancellationToken = default);
}
