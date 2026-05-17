using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Testurio.Core.Enums;
using Testurio.Core.Exceptions;
using Testurio.Core.Interfaces;
using Testurio.Core.Models;
using Testurio.Pipeline.Executors;

namespace Testurio.UnitTests.Pipeline.Executors;

/// <summary>
/// Unit tests for <see cref="HttpExecutor"/> — covers per-request timeout (feature 0022)
/// and API auth credential injection (feature 0023).
/// </summary>
public class HttpExecutorTests
{
    // ─── ApplyApiAuthCredentials — None ───────────────────────────────────────

    [Fact]
    public void ApplyApiAuthCredentials_None_AddsNoAuthHeader()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "https://example.com/api");

        HttpExecutor.ApplyApiAuthCredentials(request, new ApiTestAuthCredentials.None());

        Assert.Null(request.Headers.Authorization);
        Assert.False(request.Headers.Contains("Authorization"));
    }

    // ─── ApplyApiAuthCredentials — Bearer ─────────────────────────────────────

    [Fact]
    public void ApplyApiAuthCredentials_Bearer_AddsAuthorizationBearerHeader()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "https://example.com/api");

        HttpExecutor.ApplyApiAuthCredentials(request, new ApiTestAuthCredentials.Bearer("tok-secret"));

        Assert.NotNull(request.Headers.Authorization);
        Assert.Equal("Bearer", request.Headers.Authorization.Scheme);
        Assert.Equal("tok-secret", request.Headers.Authorization.Parameter);
    }

    // ─── ApplyApiAuthCredentials — ApiKey Header ──────────────────────────────

    [Fact]
    public void ApplyApiAuthCredentials_ApiKeyHeader_AddsCustomHeader()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "https://example.com/api");

        HttpExecutor.ApplyApiAuthCredentials(
            request,
            new ApiTestAuthCredentials.ApiKey("X-Api-Key", ApiAuthApiKeyPlacement.Header, "key-val"));

        Assert.True(request.Headers.Contains("X-Api-Key"));
        Assert.Equal("key-val", request.Headers.GetValues("X-Api-Key").First());
        Assert.Null(request.Headers.Authorization);
    }

    // ─── ApplyApiAuthCredentials — ApiKey Query (no existing query params) ────

    [Fact]
    public void ApplyApiAuthCredentials_ApiKeyQuery_AppendsKeyToUrl_WhenNoExistingQueryParams()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "https://example.com/api/users");

        HttpExecutor.ApplyApiAuthCredentials(
            request,
            new ApiTestAuthCredentials.ApiKey("api_key", ApiAuthApiKeyPlacement.Query, "my-secret"));

        var uri = request.RequestUri?.ToString() ?? string.Empty;
        Assert.Contains("?api_key=my-secret", uri);
        Assert.Null(request.Headers.Authorization);
    }

    // ─── ApplyApiAuthCredentials — ApiKey Query (existing query params) ───────

    [Fact]
    public void ApplyApiAuthCredentials_ApiKeyQuery_AppendsWithAmpersand_WhenQueryParamsExist()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "https://example.com/api?page=1");

        HttpExecutor.ApplyApiAuthCredentials(
            request,
            new ApiTestAuthCredentials.ApiKey("api_key", ApiAuthApiKeyPlacement.Query, "my-secret"));

        var uri = request.RequestUri?.ToString() ?? string.Empty;
        Assert.Contains("&api_key=my-secret", uri);
    }

    // ─── ApplyApiAuthCredentials — Basic ──────────────────────────────────────

    [Fact]
    public void ApplyApiAuthCredentials_Basic_AddsAuthorizationBasicHeader()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "https://example.com/api");

        HttpExecutor.ApplyApiAuthCredentials(
            request,
            new ApiTestAuthCredentials.Basic("user", "pass"));

        Assert.NotNull(request.Headers.Authorization);
        Assert.Equal("Basic", request.Headers.Authorization.Scheme);
        var decoded = System.Text.Encoding.UTF8.GetString(
            Convert.FromBase64String(request.Headers.Authorization.Parameter!));
        Assert.Equal("user:pass", decoded);
    }

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
