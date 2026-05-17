using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Testurio.Core.Exceptions;
using Testurio.Core.Interfaces;
using Testurio.Pipeline.Executors;

namespace Testurio.UnitTests.Pipeline.Executors;

/// <summary>
/// Unit tests for <see cref="HttpExecutor.SendWithTimeoutAsync"/> — covers the
/// per-request timeout logic introduced in feature 0022.
/// </summary>
public class HttpExecutorTests
{
    // ─── SendWithTimeoutAsync — success path ──────────────────────────────────

    [Fact]
    public async Task SendWithTimeoutAsync_ReturnsResponse_WhenRequestCompletesWithinTimeout()
    {
        var handler = new InstantResponseHandler(HttpStatusCode.OK);
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://example.com") };

        var (response, elapsedMs) = await HttpExecutor.SendWithTimeoutAsync(
            client,
            new HttpRequestMessage(HttpMethod.Get, "/api/health"),
            timeoutSeconds: 30,
            runToken: CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(elapsedMs >= 0, "ElapsedMs should be a non-negative number");
    }

    [Fact]
    public async Task SendWithTimeoutAsync_RecordsDurationMs_OnSuccess()
    {
        var handler = new DelayedResponseHandler(delay: TimeSpan.FromMilliseconds(50));
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://example.com") };

        var (_, elapsedMs) = await HttpExecutor.SendWithTimeoutAsync(
            client,
            new HttpRequestMessage(HttpMethod.Get, "/api/health"),
            timeoutSeconds: 10,
            runToken: CancellationToken.None);

        // The elapsed time should at least include the artificial delay.
        Assert.True(elapsedMs >= 40, $"Expected elapsedMs >= 40 but got {elapsedMs}");
    }

    // ─── SendWithTimeoutAsync — timeout path ──────────────────────────────────

    [Fact]
    public async Task SendWithTimeoutAsync_ThrowsTimeoutException_WhenTimeoutElapsesBeforeResponse()
    {
        // Use a handler that never completes — simulates a hung endpoint.
        var handler = new NeverRespondingHandler();
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://example.com") };

        var ex = await Assert.ThrowsAsync<TimeoutException>(() =>
            HttpExecutor.SendWithTimeoutAsync(
                client,
                new HttpRequestMessage(HttpMethod.Get, "/slow"),
                timeoutSeconds: 1,
                runToken: CancellationToken.None));

        Assert.Contains("Timeout", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("1s", ex.Message);
    }

    [Fact]
    public async Task SendWithTimeoutAsync_TimeoutMessage_IncludesConfiguredTimeoutValue()
    {
        var handler = new NeverRespondingHandler();
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://example.com") };

        var ex = await Assert.ThrowsAsync<TimeoutException>(() =>
            HttpExecutor.SendWithTimeoutAsync(
                client,
                new HttpRequestMessage(HttpMethod.Get, "/slow"),
                timeoutSeconds: 5,
                runToken: CancellationToken.None));

        Assert.Contains("5s", ex.Message);
    }

    [Fact]
    public async Task SendWithTimeoutAsync_DoesNotThrowTimeout_WhenRunTokenCancelledFirst()
    {
        // When the run-level CancellationToken is cancelled first, the method should
        // propagate OperationCanceledException (not TimeoutException), so callers can
        // distinguish a run-level cancellation from a per-request timeout.
        var handler = new NeverRespondingHandler();
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://example.com") };

        using var runCts = new CancellationTokenSource();
        runCts.CancelAfter(TimeSpan.FromMilliseconds(50));

        // timeoutSeconds is large enough that per-request timeout won't fire first.
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            HttpExecutor.SendWithTimeoutAsync(
                client,
                new HttpRequestMessage(HttpMethod.Get, "/slow"),
                timeoutSeconds: 60,
                runToken: runCts.Token));
    }

    // ─── Helper message handlers ──────────────────────────────────────────────

    private sealed class InstantResponseHandler(HttpStatusCode statusCode) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(statusCode));
    }

    private sealed class DelayedResponseHandler(TimeSpan delay) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await Task.Delay(delay, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }

    private sealed class NeverRespondingHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // Block until the token is cancelled.
            await Task.Delay(Timeout.Infinite, cancellationToken);
            // This line is unreachable; required to satisfy the return type.
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }
}
