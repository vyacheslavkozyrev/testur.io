using Microsoft.Extensions.Logging.Abstractions;
using Testurio.Core.Enums;
using Testurio.Core.Events;
using Testurio.Core.Models;
using Testurio.Infrastructure.Sse;

namespace Testurio.UnitTests.Services;

/// <summary>
/// Unit tests for <see cref="DashboardStreamManager"/>.
/// All tests operate against the in-memory channel directly — no mocks required.
/// </summary>
public class DashboardStreamManagerTests
{
    private static DashboardStreamManager CreateSut() =>
        new(NullLogger<DashboardStreamManager>.Instance);

    private static DashboardUpdatedEvent MakeEvent(string projectId = "proj-1") =>
        new(projectId, new LatestRunSummary("run-1", RunStatus.Passed, DateTimeOffset.UtcNow, null));

    // ─── PublishAsync routes to the correct user channel ─────────────────────

    [Fact]
    public async Task PublishAsync_RoutesToCorrectUserChannel()
    {
        var sut = CreateSut();
        var @event = MakeEvent("proj-A");

        await sut.PublishAsync("user-1", @event);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var received = await sut.StreamAsync("user-1", cts.Token).FirstAsync(cts.Token);

        Assert.Equal("proj-A", received.ProjectId);
    }

    // ─── Unknown userId creates a new channel on first publish ───────────────

    [Fact]
    public async Task PublishAsync_UnknownUserId_CreatesNewChannelOnFirstPublish()
    {
        var sut = CreateSut();
        var @event = MakeEvent();

        // No channel for "new-user" exists yet — should not throw.
        await sut.PublishAsync("new-user", @event);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var received = await sut.StreamAsync("new-user", cts.Token).FirstAsync(cts.Token);

        Assert.Equal(@event.ProjectId, received.ProjectId);
    }

    // ─── StreamAsync yields events in insertion order ────────────────────────

    [Fact]
    public async Task StreamAsync_YieldsEventsInInsertionOrder()
    {
        var sut = CreateSut();

        await sut.PublishAsync("user-order", MakeEvent("proj-1"));
        await sut.PublishAsync("user-order", MakeEvent("proj-2"));
        await sut.PublishAsync("user-order", MakeEvent("proj-3"));

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        var results = new List<string>();
        await foreach (var evt in sut.StreamAsync("user-order", cts.Token))
        {
            results.Add(evt.ProjectId);
            if (results.Count == 3) break;
        }

        Assert.Equal(["proj-1", "proj-2", "proj-3"], results);
    }

    // ─── Concurrent publishes from multiple users do not cross channels ───────

    [Fact]
    public async Task PublishAsync_ConcurrentPublishes_DoNotCrossChannels()
    {
        var sut = CreateSut();

        // Publish 10 events concurrently to two different users.
        var tasks = Enumerable.Range(0, 10).SelectMany(i => new[]
        {
            sut.PublishAsync("user-alpha", MakeEvent($"alpha-{i}")),
            sut.PublishAsync("user-beta",  MakeEvent($"beta-{i}")),
        });

        await Task.WhenAll(tasks);

        // Drain both channels and verify no cross-contamination.
        var alphaEvents = await DrainAsync(sut, "user-alpha", 10);
        var betaEvents  = await DrainAsync(sut, "user-beta",  10);

        Assert.All(alphaEvents, e => Assert.StartsWith("alpha-", e.ProjectId));
        Assert.All(betaEvents,  e => Assert.StartsWith("beta-",  e.ProjectId));
    }

    private static async Task<List<DashboardUpdatedEvent>> DrainAsync(
        DashboardStreamManager sut,
        string userId,
        int count)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var results = new List<DashboardUpdatedEvent>(count);

        await foreach (var evt in sut.StreamAsync(userId, cts.Token))
        {
            results.Add(evt);
            if (results.Count == count) break;
        }

        return results;
    }
}

/// <summary>
/// Provides a <c>FirstAsync</c> extension on <see cref="IAsyncEnumerable{T}"/>
/// so tests can read one item without importing System.Linq.Async.
/// </summary>
file static class AsyncEnumerableExtensions
{
    public static async ValueTask<T> FirstAsync<T>(
        this IAsyncEnumerable<T> source,
        CancellationToken cancellationToken)
    {
        await foreach (var item in source.WithCancellation(cancellationToken))
            return item;

        throw new InvalidOperationException("Sequence contains no elements.");
    }
}
