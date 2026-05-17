using Testurio.Core.Events;

namespace Testurio.Core.Interfaces;

/// <summary>
/// Manages per-user in-memory SSE channels for dashboard real-time updates.
/// Registered as a singleton so channels survive across HTTP requests.
/// </summary>
public interface IDashboardStreamManager
{
    /// <summary>
    /// Publishes a <see cref="DashboardUpdatedEvent"/> to the channel for <paramref name="userId"/>.
    /// Creates the channel if it does not yet exist.
    /// </summary>
    Task PublishAsync(string userId, DashboardUpdatedEvent @event, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns an async stream of <see cref="DashboardUpdatedEvent"/> items for <paramref name="userId"/>.
    /// Yields until <paramref name="cancellationToken"/> is cancelled (i.e. the SSE connection closes).
    /// </summary>
    IAsyncEnumerable<DashboardUpdatedEvent> StreamAsync(string userId, CancellationToken cancellationToken);
}
