using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Testurio.Core.Events;
using Testurio.Core.Interfaces;

namespace Testurio.Infrastructure.Sse;

/// <summary>
/// In-memory, singleton SSE fan-out manager.
/// Holds one <see cref="Channel{T}"/> per authenticated user; multiple browser tabs
/// for the same user each call <see cref="StreamAsync"/> and read independently.
/// </summary>
public sealed partial class DashboardStreamManager : IDashboardStreamManager
{
    // Bounded capacity of 64 prevents runaway memory if a subscriber stops reading.
    // FullMode.DropOldest drops the stale head item rather than blocking the publisher.
    private const int ChannelCapacity = 64;

    private readonly ConcurrentDictionary<string, Channel<DashboardUpdatedEvent>> _channels = new();
    private readonly ILogger<DashboardStreamManager> _logger;

    public DashboardStreamManager(ILogger<DashboardStreamManager> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task PublishAsync(
        string userId,
        DashboardUpdatedEvent @event,
        CancellationToken cancellationToken = default)
    {
        var channel = _channels.GetOrAdd(userId, _ => CreateChannel());

        // TryWrite returns false only when the channel is full (DropOldest handles that internally,
        // so this path is a safety net for a synchronously completed WriteAsync).
        if (!channel.Writer.TryWrite(@event))
        {
            await channel.Writer.WriteAsync(@event, cancellationToken);
        }

        LogPublished(_logger, userId, @event.ProjectId);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<DashboardUpdatedEvent> StreamAsync(
        string userId,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var channel = _channels.GetOrAdd(userId, _ => CreateChannel());

        await foreach (var @event in channel.Reader.ReadAllAsync(cancellationToken))
        {
            yield return @event;
        }
    }

    private static Channel<DashboardUpdatedEvent> CreateChannel() =>
        Channel.CreateBounded<DashboardUpdatedEvent>(new BoundedChannelOptions(ChannelCapacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleWriter = false,
            SingleReader = false,
        });

    [LoggerMessage(Level = LogLevel.Debug, Message = "Published DashboardUpdatedEvent for user {UserId}, project {ProjectId}")]
    private static partial void LogPublished(ILogger logger, string userId, string projectId);
}
