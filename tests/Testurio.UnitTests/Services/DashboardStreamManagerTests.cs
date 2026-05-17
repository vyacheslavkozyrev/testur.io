using Microsoft.Extensions.Logging.Abstractions;
using Testurio.Core.Enums;
using Testurio.Core.Events;
using Testurio.Core.Models;
using Testurio.Infrastructure.Sse;

namespace Testurio.UnitTests.Services;

/// <summary>
/// Unit tests for <see cref="DashboardStreamManager"/>.
/// All tests operate against the in-memory channel directly — no mocks required.
///
/// Fan-out design: each call to StreamAsync registers a private per-connection channel.
/// PublishAsync fans the event out to every active channel for that user.
/// Tests must therefore start streaming before publishing so the channel is registered.
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
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        // Start the stream before publishing so the per-connection channel is registered.
        var streamTask = Task.Run(async () =>
            await sut.StreamAsync("user-1", cts.Token).FirstAsync(cts.Token), cts.Token);

        // Brief delay to let StreamAsync register the channel.
        await Task.Delay(50, cts.Token);

        await sut.PublishAsync("user-1", MakeEvent("proj-A"));

        var received = await streamTask;
        Assert.Equal("proj-A", received.ProjectId);
    }

    // ─── Events published with no active subscriber are dropped (not buffered) ─

    [Fact]
    public async Task PublishAsync_WithNoActiveSubscribers_DropsEventGracefully()
    {
        var sut = CreateSut();

        // No StreamAsync call yet — should not throw.
        await sut.PublishAsync("no-subscriber-user", MakeEvent());

        // Events are dropped; subsequent StreamAsync starts clean.
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        var results = new List<DashboardUpdatedEvent>();

        try
        {
            await foreach (var evt in sut.StreamAsync("no-subscriber-user", cts.Token))
                results.Add(evt);
        }
        catch (OperationCanceledException) { /* expected timeout */ }

        Assert.Empty(results);
    }

    // ─── StreamAsync yields events in insertion order ────────────────────────

    [Fact]
    public async Task StreamAsync_YieldsEventsInInsertionOrder()
    {
        var sut = CreateSut();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        var results = new List<string>();
        var streamTask = Task.Run(async () =>
        {
            await foreach (var evt in sut.StreamAsync("user-order", cts.Token))
            {
                results.Add(evt.ProjectId);
                if (results.Count == 3) break;
            }
        }, cts.Token);

        await Task.Delay(50, cts.Token);

        await sut.PublishAsync("user-order", MakeEvent("proj-1"));
        await sut.PublishAsync("user-order", MakeEvent("proj-2"));
        await sut.PublishAsync("user-order", MakeEvent("proj-3"));

        await streamTask;

        Assert.Equal(["proj-1", "proj-2", "proj-3"], results);
    }

    // ─── Concurrent publishes from multiple users do not cross channels ───────

    [Fact]
    public async Task PublishAsync_ConcurrentPublishes_DoNotCrossChannels()
    {
        var sut = CreateSut();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        // Start streaming for both users first.
        var alphaTask = Task.Run(() => DrainAsync(sut, "user-alpha", 10, cts.Token), cts.Token);
        var betaTask  = Task.Run(() => DrainAsync(sut, "user-beta",  10, cts.Token), cts.Token);

        // Brief delay to let both StreamAsync calls register their channels.
        await Task.Delay(50, cts.Token);

        // Publish 10 events concurrently to two different users.
        var publishTasks = Enumerable.Range(0, 10).SelectMany(i => new[]
        {
            sut.PublishAsync("user-alpha", MakeEvent($"alpha-{i}")),
            sut.PublishAsync("user-beta",  MakeEvent($"beta-{i}")),
        });

        await Task.WhenAll(publishTasks);

        var alphaEvents = await alphaTask;
        var betaEvents  = await betaTask;

        Assert.All(alphaEvents, e => Assert.StartsWith("alpha-", e.ProjectId));
        Assert.All(betaEvents,  e => Assert.StartsWith("beta-",  e.ProjectId));
    }

    // ─── Multi-tab fan-out: two connections for the same user each receive every event ─

    [Fact]
    public async Task PublishAsync_MultipleActiveConnections_FansOutToAll()
    {
        var sut = CreateSut();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        // Simulate two browser tabs: each starts an independent StreamAsync call.
        var tab1Task = Task.Run(async () =>
            await sut.StreamAsync("shared-user", cts.Token).FirstAsync(cts.Token), cts.Token);
        var tab2Task = Task.Run(async () =>
            await sut.StreamAsync("shared-user", cts.Token).FirstAsync(cts.Token), cts.Token);

        await Task.Delay(50, cts.Token);

        var @event = MakeEvent("proj-fanout");
        await sut.PublishAsync("shared-user", @event);

        var tab1Result = await tab1Task;
        var tab2Result = await tab2Task;

        // Both tabs must receive the same event — not competing consumers.
        Assert.Equal("proj-fanout", tab1Result.ProjectId);
        Assert.Equal("proj-fanout", tab2Result.ProjectId);
    }

    // ─── Channel is cleaned up when StreamAsync is cancelled ─────────────────

    [Fact]
    public async Task StreamAsync_RemovesChannelOnCancellation()
    {
        var sut = CreateSut();
        using var cts = new CancellationTokenSource();

        var streamTask = Task.Run(async () =>
        {
            try
            {
                await foreach (var _ in sut.StreamAsync("cleanup-user", cts.Token)) { }
            }
            catch (OperationCanceledException) { /* expected */ }
        });

        await Task.Delay(50);

        // Cancel the connection.
        await cts.CancelAsync();
        await streamTask;

        // After cancellation, publishing to that user should not throw even though the
        // channel was removed. A new StreamAsync call would register a fresh channel.
        await sut.PublishAsync("cleanup-user", MakeEvent());
    }

    private static async Task<List<DashboardUpdatedEvent>> DrainAsync(
        DashboardStreamManager sut,
        string userId,
        int count,
        CancellationToken cancellationToken)
    {
        var results = new List<DashboardUpdatedEvent>(count);

        await foreach (var evt in sut.StreamAsync(userId, cancellationToken))
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
