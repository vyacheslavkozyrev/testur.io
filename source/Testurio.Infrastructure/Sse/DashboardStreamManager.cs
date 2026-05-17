using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Testurio.Core.Events;
using Testurio.Core.Interfaces;

namespace Testurio.Infrastructure.Sse;

/// <summary>
/// In-memory, singleton SSE fan-out manager.
/// Holds a list of per-connection <see cref="Channel{T}"/> instances per authenticated user.
/// Each call to <see cref="StreamAsync"/> registers its own private channel so that multiple
/// browser tabs for the same user each receive every event independently (fan-out, not
/// competing consumers). Channels are removed when the connection cancellation token fires.
/// </summary>
public sealed partial class DashboardStreamManager : IDashboardStreamManager
{
    // Bounded capacity of 64 prevents runaway memory if a subscriber stops reading.
    // FullMode.DropOldest drops the stale head item rather than blocking the publisher.
    private const int ChannelCapacity = 64;

    // Per-user list of active per-connection channels.
    // ConcurrentBag is append/take; we need enumeration + removal, so use a list guarded by the bag itself.
    // We use ConcurrentDictionary<userId, ConcurrentDictionary<connectionId, Channel<T>>> for O(1) removal.
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<Guid, Channel<DashboardUpdatedEvent>>> _userChannels = new();
    private readonly ILogger<DashboardStreamManager> _logger;

    public DashboardStreamManager(ILogger<DashboardStreamManager> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task PublishAsync(
        string userId,
        DashboardUpdatedEvent @event,
        CancellationToken cancellationToken = default)
    {
        if (!_userChannels.TryGetValue(userId, out var connections) || connections.IsEmpty)
        {
            // No active connections for this user — create a bucket so future StreamAsync calls
            // can pick up events published before the first connection opens.
            // We do NOT create a dangling channel here; events are dropped if no subscriber is present.
            LogNoSubscribers(_logger, userId);
            return Task.CompletedTask;
        }

        foreach (var (_, channel) in connections)
        {
            if (!channel.Writer.TryWrite(@event))
            {
                // Channel is full — WriteAsync will block until capacity is available or drops oldest.
                // We use a best-effort fire-and-forget per connection so one slow tab does not block others.
                _ = channel.Writer.WriteAsync(@event, cancellationToken).AsTask()
                    .ContinueWith(
                        t => LogWriteFailed(_logger, userId, t.Exception?.InnerException),
                        TaskContinuationOptions.OnlyOnFaulted);
            }
        }

        LogPublished(_logger, userId, @event.ProjectId, connections.Count);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<DashboardUpdatedEvent> StreamAsync(
        string userId,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // Each connection gets its own private channel.
        var connectionId = Guid.NewGuid();
        var channel = CreateChannel();

        var connections = _userChannels.GetOrAdd(userId, _ => new ConcurrentDictionary<Guid, Channel<DashboardUpdatedEvent>>());
        connections[connectionId] = channel;

        try
        {
            await foreach (var @event in channel.Reader.ReadAllAsync(cancellationToken))
            {
                yield return @event;
            }
        }
        finally
        {
            // Remove this connection's channel when the SSE connection closes.
            connections.TryRemove(connectionId, out _);
        }
    }

    private static Channel<DashboardUpdatedEvent> CreateChannel() =>
        Channel.CreateBounded<DashboardUpdatedEvent>(new BoundedChannelOptions(ChannelCapacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleWriter = false,
            SingleReader = true, // Each channel has exactly one reader (one SSE connection).
        });

    [LoggerMessage(Level = LogLevel.Debug, Message = "Published DashboardUpdatedEvent for user {UserId}, project {ProjectId} to {ConnectionCount} connection(s)")]
    private static partial void LogPublished(ILogger logger, string userId, string projectId, int connectionCount);

    [LoggerMessage(Level = LogLevel.Debug, Message = "No active SSE subscribers for user {UserId} — event dropped")]
    private static partial void LogNoSubscribers(ILogger logger, string userId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to write DashboardUpdatedEvent to channel for user {UserId}")]
    private static partial void LogWriteFailed(ILogger logger, string userId, Exception? ex);
}
