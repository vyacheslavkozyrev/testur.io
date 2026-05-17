using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Testurio.Core.Entities;
using Testurio.Core.Interfaces;
using Testurio.Core.Models;
using Testurio.Pipeline.Executors;

namespace Testurio.UnitTests.Pipeline.Executors;

/// <summary>
/// Unit tests for <see cref="HttpExecutor"/> covering all assertion types and edge cases
/// defined in feature 0029 acceptance criteria (AC-007 through AC-017), plus the
/// per-request timeout logic from <see cref="HttpExecutor.SendWithTimeoutAsync"/>.
/// HTTP responses are injected via a custom <see cref="DelegatingHandler"/> — no real network calls.
/// </summary>
public class HttpExecutorTests
{
    private readonly Mock<IProjectAccessCredentialProvider> _credentialProvider = new();
    private static readonly Project DefaultProject = new()
    {
        UserId = "user1",
        Name = "Test",
        ProductUrl = "https://api.example.com",
        TestingStrategy = "api"
    };

    public HttpExecutorTests()
    {
        _credentialProvider
            .Setup(p => p.ResolveAsync(It.IsAny<Project>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProjectAccessCredentials.IpAllowlist());
    }

    private HttpExecutor CreateSut(HttpResponseMessage response)
    {
        var handler = new StaticResponseHandler(response);
        var httpClient = new HttpClient(handler);
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        return new HttpExecutor(
            _credentialProvider.Object,
            factory.Object,
            NullLogger<HttpExecutor>.Instance);
    }

    private static ApiTestScenario MakeScenario(
        IReadOnlyList<Assertion> assertions,
        string method = "GET",
        string path = "/items")
        => new()
        {
            Id = "sc1",
            Title = "Test scenario",
            Method = method,
            Path = path,
            Assertions = assertions
        };

    // ─── AC-011: status_code assertion ───────────────────────────────────────

    [Fact]
    public async Task StatusCodeAssertion_MatchingCode_PassesAndPopulatesActual()
    {
        var sut = CreateSut(new HttpResponseMessage(HttpStatusCode.OK));
        var scenario = MakeScenario([new StatusCodeAssertion { Expected = 200 }]);

        var results = await sut.ExecuteAsync([scenario], DefaultProject);

        var ar = results[0].AssertionResults[0];
        Assert.True(results[0].Passed);
        Assert.True(ar.Passed);
        Assert.Equal("200", ar.Actual);
    }

    [Fact]
    public async Task StatusCodeAssertion_MismatchedCode_FailsAndPopulatesActual()
    {
        var sut = CreateSut(new HttpResponseMessage(HttpStatusCode.NotFound));
        var scenario = MakeScenario([new StatusCodeAssertion { Expected = 200 }]);

        var results = await sut.ExecuteAsync([scenario], DefaultProject);

        var ar = results[0].AssertionResults[0];
        Assert.False(results[0].Passed);
        Assert.False(ar.Passed);
        Assert.Equal("404", ar.Actual);
        Assert.Equal("200", ar.Expected);
    }

    // ─── AC-012: json_path assertion ─────────────────────────────────────────

    [Fact]
    public async Task JsonPathAssertion_MatchingValue_Passes()
    {
        var body = """{"data":{"id":"abc123"}}""";
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        var sut = CreateSut(response);
        var scenario = MakeScenario([new JsonPathAssertion { Path = "$.data.id", Expected = "abc123" }]);

        var results = await sut.ExecuteAsync([scenario], DefaultProject);

        Assert.True(results[0].Passed);
        Assert.Equal("abc123", results[0].AssertionResults[0].Actual);
    }

    [Fact]
    public async Task JsonPathAssertion_NonMatchingValue_Fails()
    {
        var body = """{"data":{"id":"xyz"}}""";
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        var sut = CreateSut(response);
        var scenario = MakeScenario([new JsonPathAssertion { Path = "$.data.id", Expected = "abc123" }]);

        var results = await sut.ExecuteAsync([scenario], DefaultProject);

        Assert.False(results[0].Passed);
        Assert.Equal("xyz", results[0].AssertionResults[0].Actual);
    }

    [Fact]
    public async Task JsonPathAssertion_PathNotFound_ReturnsNoMatchAndFails()
    {
        var body = """{"other":"value"}""";
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        var sut = CreateSut(response);
        var scenario = MakeScenario([new JsonPathAssertion { Path = "$.data.id", Expected = "abc123" }]);

        var results = await sut.ExecuteAsync([scenario], DefaultProject);

        Assert.False(results[0].Passed);
        Assert.Equal("<no match>", results[0].AssertionResults[0].Actual);
    }

    [Fact]
    public async Task JsonPathAssertion_WildcardExpected_PassesWhenPathResolves()
    {
        var body = """{"item":"something"}""";
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        var sut = CreateSut(response);
        var scenario = MakeScenario([new JsonPathAssertion { Path = "$.item", Expected = "*" }]);

        var results = await sut.ExecuteAsync([scenario], DefaultProject);

        Assert.True(results[0].Passed);
    }

    [Fact]
    public async Task JsonPathAssertion_WildcardExpected_FailsWhenPathDoesNotResolve()
    {
        var body = """{"other":"value"}""";
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        var sut = CreateSut(response);
        var scenario = MakeScenario([new JsonPathAssertion { Path = "$.item", Expected = "*" }]);

        var results = await sut.ExecuteAsync([scenario], DefaultProject);

        Assert.False(results[0].Passed);
        Assert.Equal("<no match>", results[0].AssertionResults[0].Actual);
    }

    // ─── AC-013: header assertion ─────────────────────────────────────────────

    [Fact]
    public async Task HeaderAssertion_MatchingHeader_Passes()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK);
        response.Headers.Add("X-Custom-Header", "expected-value");
        var sut = CreateSut(response);
        var scenario = MakeScenario([new HeaderAssertion { Name = "X-Custom-Header", Expected = "expected-value" }]);

        var results = await sut.ExecuteAsync([scenario], DefaultProject);

        Assert.True(results[0].Passed);
        Assert.Equal("expected-value", results[0].AssertionResults[0].Actual);
    }

    [Fact]
    public async Task HeaderAssertion_CaseInsensitiveMatch_Passes()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK);
        response.Headers.Add("X-Custom-Header", "EXPECTED-VALUE");
        var sut = CreateSut(response);
        var scenario = MakeScenario([new HeaderAssertion { Name = "X-Custom-Header", Expected = "expected-value" }]);

        var results = await sut.ExecuteAsync([scenario], DefaultProject);

        Assert.True(results[0].Passed);
    }

    [Fact]
    public async Task HeaderAssertion_AbsentHeader_ReturnsAbsentAndFails()
    {
        var sut = CreateSut(new HttpResponseMessage(HttpStatusCode.OK));
        var scenario = MakeScenario([new HeaderAssertion { Name = "X-Missing", Expected = "value" }]);

        var results = await sut.ExecuteAsync([scenario], DefaultProject);

        Assert.False(results[0].Passed);
        Assert.Equal("<absent>", results[0].AssertionResults[0].Actual);
    }

    // ─── AC-010: all assertions evaluated even when one fails ─────────────────

    [Fact]
    public async Task AllAssertionsEvaluated_EvenWhenFirstFails()
    {
        var body = """{"id":"1"}""";
        var response = new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        var sut = CreateSut(response);
        var scenario = MakeScenario(
        [
            new StatusCodeAssertion { Expected = 200 },           // fails
            new JsonPathAssertion { Path = "$.id", Expected = "1" }  // passes
        ]);

        var results = await sut.ExecuteAsync([scenario], DefaultProject);

        Assert.False(results[0].Passed);
        Assert.Equal(2, results[0].AssertionResults.Count);
        Assert.False(results[0].AssertionResults[0].Passed);
        Assert.True(results[0].AssertionResults[1].Passed);
    }

    // ─── AC-015: DurationMs populated ─────────────────────────────────────────

    [Fact]
    public async Task DurationMs_IsAlwaysPopulated()
    {
        var sut = CreateSut(new HttpResponseMessage(HttpStatusCode.OK));
        var scenario = MakeScenario([new StatusCodeAssertion { Expected = 200 }]);

        var results = await sut.ExecuteAsync([scenario], DefaultProject);

        Assert.True(results[0].DurationMs >= 0);
    }

    // ─── AC-016: HTTP request failure marks all assertions failed ─────────────

    [Fact]
    public async Task HttpRequestException_MarksAllAssertionsFailed()
    {
        var handler = new ThrowingHandler(new HttpRequestException("Connection refused"));
        var httpClient = new HttpClient(handler);
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var sut = new HttpExecutor(
            _credentialProvider.Object,
            factory.Object,
            NullLogger<HttpExecutor>.Instance);

        var scenario = MakeScenario(
        [
            new StatusCodeAssertion { Expected = 200 },
            new JsonPathAssertion { Path = "$.id", Expected = "1" }
        ]);

        var results = await sut.ExecuteAsync([scenario], DefaultProject);

        Assert.False(results[0].Passed);
        Assert.All(results[0].AssertionResults, ar =>
        {
            Assert.False(ar.Passed);
            Assert.Contains("Connection refused", ar.Actual);
        });
    }

    // ─── AC-017: sequential execution, one failure does not skip others ───────

    [Fact]
    public async Task SequentialExecution_FailedScenarioDoesNotPreventSubsequentScenarios()
    {
        var callCount = 0;
        var handler = new DelegateHandler(_ =>
        {
            callCount++;
            return callCount == 1
                ? new HttpResponseMessage(HttpStatusCode.InternalServerError)
                : new HttpResponseMessage(HttpStatusCode.OK);
        });
        var httpClient = new HttpClient(handler);
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var sut = new HttpExecutor(
            _credentialProvider.Object,
            factory.Object,
            NullLogger<HttpExecutor>.Instance);

        var scenarios = new List<ApiTestScenario>
        {
            MakeScenario([new StatusCodeAssertion { Expected = 200 }], path: "/s1"),
            MakeScenario([new StatusCodeAssertion { Expected = 200 }], path: "/s2")
        };

        var results = await sut.ExecuteAsync(scenarios, DefaultProject);

        Assert.Equal(2, results.Count);
        Assert.False(results[0].Passed);
        Assert.True(results[1].Passed);
        Assert.Equal(2, callCount);
    }

    // ─── AC-007/AC-008/AC-009: request construction ───────────────────────────

    [Fact]
    public async Task PostRequest_SendsBodyAsJson()
    {
        HttpRequestMessage? captured = null;
        var handler = new CaptureRequestHandler(req =>
        {
            captured = req;
            return new HttpResponseMessage(HttpStatusCode.Created);
        });
        var httpClient = new HttpClient(handler);
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var sut = new HttpExecutor(
            _credentialProvider.Object,
            factory.Object,
            NullLogger<HttpExecutor>.Instance);

        var scenario = new ApiTestScenario
        {
            Id = "s1",
            Title = "Create item",
            Method = "POST",
            Path = "/items",
            Body = new { name = "Widget" },
            Assertions = [new StatusCodeAssertion { Expected = 201 }]
        };

        await sut.ExecuteAsync([scenario], DefaultProject);

        Assert.NotNull(captured);
        Assert.Equal(HttpMethod.Post, captured!.Method);
        Assert.Equal("application/json", captured.Content?.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task CustomHeaders_AddedToRequest()
    {
        HttpRequestMessage? captured = null;
        var handler = new CaptureRequestHandler(req =>
        {
            captured = req;
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        var httpClient = new HttpClient(handler);
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var sut = new HttpExecutor(
            _credentialProvider.Object,
            factory.Object,
            NullLogger<HttpExecutor>.Instance);

        var scenario = new ApiTestScenario
        {
            Id = "s1",
            Title = "Get with header",
            Method = "GET",
            Path = "/items",
            Headers = new Dictionary<string, string> { ["X-Tenant-Id"] = "tenant1" },
            Assertions = [new StatusCodeAssertion { Expected = 200 }]
        };

        await sut.ExecuteAsync([scenario], DefaultProject);

        Assert.NotNull(captured);
        Assert.True(captured!.Headers.Contains("X-Tenant-Id"));
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

        Assert.True(elapsedMs >= 40, $"Expected elapsedMs >= 40 but got {elapsedMs}");
    }

    // ─── SendWithTimeoutAsync — timeout path ──────────────────────────────────

    [Fact]
    public async Task SendWithTimeoutAsync_ThrowsTimeoutException_WhenTimeoutElapsesBeforeResponse()
    {
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
        var handler = new NeverRespondingHandler();
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://example.com") };

        using var runCts = new CancellationTokenSource();
        runCts.CancelAfter(TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            HttpExecutor.SendWithTimeoutAsync(
                client,
                new HttpRequestMessage(HttpMethod.Get, "/slow"),
                timeoutSeconds: 60,
                runToken: runCts.Token));
    }

    // ─── Helper types ─────────────────────────────────────────────────────────

    private sealed class StaticResponseHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(response);
    }

    private sealed class ThrowingHandler(Exception exception) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => throw exception;
    }

    private sealed class DelegateHandler(Func<HttpRequestMessage, HttpResponseMessage> factory) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(factory(request));
    }

    private sealed class CaptureRequestHandler(Func<HttpRequestMessage, HttpResponseMessage> factory)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(factory(request));
    }

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
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }
}
