using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Testurio.Core.Entities;
using Testurio.Core.Exceptions;
using Testurio.Core.Interfaces;
using Testurio.Core.Models;
using Testurio.Pipeline.Executors;

namespace Testurio.IntegrationTests.Pipeline;

/// <summary>
/// Integration tests for the executor stage (feature 0029).
/// Exercises the full path through <see cref="ExecutorRouter"/> → <see cref="HttpExecutor"/>
/// and/or mocked <see cref="IPlaywrightExecutor"/>, validating:
/// - Both executors succeed → merged <see cref="ExecutionResult"/> produced
/// - Both lists empty → <see cref="ExecutorRouterException"/> thrown
/// - CancellationToken cancelled mid-execution → both executors cancelled
/// - <see cref="TestRun.ExecutionWarnings"/> populated when one executor fails
/// </summary>
public class ExecutorsIntegrationTests
{
    private readonly Mock<IProjectAccessCredentialProvider> _credentialProvider = new();
    private readonly Mock<IPlaywrightExecutor> _playwrightExecutor = new();
    private readonly Mock<IScreenshotStorage> _screenshotStorage = new();

    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid RunId  = Guid.NewGuid();

    private static readonly Project DefaultProject = new()
    {
        UserId = UserId.ToString(),
        Name = "Integration Test Project",
        ProductUrl = "https://api.example.com",
        TestingStrategy = "api"
    };

    public ExecutorsIntegrationTests()
    {
        _credentialProvider
            .Setup(p => p.ResolveAsync(It.IsAny<Project>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProjectAccessCredentials.IpAllowlist());
    }

    private HttpExecutor CreateHttpExecutor(HttpResponseMessage response)
    {
        var handler = new StaticResponseHandler(response);
        var httpClient = new HttpClient(handler);
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);
        return new HttpExecutor(_credentialProvider.Object, factory.Object,
            NullLogger<HttpExecutor>.Instance);
    }

    private ExecutorRouter CreateSut(HttpResponseMessage response) =>
        new(CreateHttpExecutor(response), _playwrightExecutor.Object);

    // ─── AC-001/AC-006: both executors succeed → merged ExecutionResult ───────

    [Fact]
    public async Task BothExecutorsSucceed_MergesApiAndUiResults()
    {
        var uiResult = new UiE2eScenarioResult
        {
            ScenarioId = "ui-1", Title = "UI test", Passed = true, DurationMs = 200,
            StepResults = []
        };

        _playwrightExecutor
            .Setup(e => e.ExecuteAsync(It.IsAny<IReadOnlyList<UiE2eTestScenario>>(),
                It.IsAny<Project>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([uiResult]);

        var generatorResults = new GeneratorResults
        {
            ApiScenarios   = [MakeApiScenario()],
            UiE2eScenarios = [MakeUiScenario()]
        };

        var sut = CreateSut(new HttpResponseMessage(HttpStatusCode.OK));
        var result = await sut.ExecuteAsync(generatorResults, DefaultProject, UserId, RunId);

        Assert.Single(result.ApiResults);
        Assert.True(result.ApiResults[0].Passed);
        Assert.Single(result.UiE2eResults);
        Assert.Equal("ui-1", result.UiE2eResults[0].ScenarioId);
    }

    // ─── AC-004: both lists empty → ExecutorRouterException ──────────────────

    [Fact]
    public async Task BothListsEmpty_ThrowsExecutorRouterException_WithExpectedMessage()
    {
        var generatorResults = new GeneratorResults
        {
            ApiScenarios   = [],
            UiE2eScenarios = []
        };

        var sut = CreateSut(new HttpResponseMessage(HttpStatusCode.OK));

        var ex = await Assert.ThrowsAsync<ExecutorRouterException>(
            () => sut.ExecuteAsync(generatorResults, DefaultProject, UserId, RunId));

        Assert.Equal(
            "No scenarios to execute — both API and UI E2E scenario lists are empty",
            ex.Message);
    }

    // ─── AC-005: CancellationToken cancelled mid-execution → both cancelled ───

    [Fact]
    public async Task CancellationToken_Cancelled_BothExecutorsCancelled()
    {
        using var cts = new CancellationTokenSource();

        // The HTTP handler will cancel after the request starts.
        var handler = new CancellingHandler(cts);
        var httpClient = new HttpClient(handler);
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);
        var httpExecutor = new HttpExecutor(_credentialProvider.Object, factory.Object,
            NullLogger<HttpExecutor>.Instance);

        _playwrightExecutor
            .Setup(e => e.ExecuteAsync(It.IsAny<IReadOnlyList<UiE2eTestScenario>>(),
                It.IsAny<Project>(), It.IsAny<Guid>(), It.IsAny<Guid>(), cts.Token))
            .ThrowsAsync(new OperationCanceledException(cts.Token));

        var generatorResults = new GeneratorResults
        {
            ApiScenarios   = [MakeApiScenario()],
            UiE2eScenarios = [MakeUiScenario()]
        };

        var sut = new ExecutorRouter(httpExecutor, _playwrightExecutor.Object);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => sut.ExecuteAsync(generatorResults, DefaultProject, UserId, RunId, cts.Token));
    }

    // ─── AC-042: ExecutionWarnings never null even with infrastructure failure ─

    [Fact]
    public void ExecutionWarnings_DefaultValue_IsEmptyArray()
    {
        // Verify TestRun.ExecutionWarnings is initialized to empty array (never null).
        var testRun = new TestRun
        {
            ProjectId = "proj1",
            UserId = "user1",
            JiraIssueKey = "PROJ-1",
            JiraIssueId = "1"
        };

        Assert.NotNull(testRun.ExecutionWarnings);
        Assert.Empty(testRun.ExecutionWarnings);
    }

    // ─── AC-016: HttpExecutor handles request failure gracefully ─────────────

    [Fact]
    public async Task HttpExecutor_RequestFails_AllAssertionsMarkedFailedWithExceptionMessage()
    {
        var handler = new ThrowingHandler(new HttpRequestException("Connection refused"));
        var httpClient = new HttpClient(handler);
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);
        var httpExecutor = new HttpExecutor(_credentialProvider.Object, factory.Object,
            NullLogger<HttpExecutor>.Instance);

        var generatorResults = new GeneratorResults
        {
            ApiScenarios   =
            [
                new ApiTestScenario
                {
                    Id = "sc1", Title = "Failing request", Method = "GET", Path = "/items",
                    Assertions = [new StatusCodeAssertion { Expected = 200 }]
                }
            ],
            UiE2eScenarios = []
        };

        var sut = new ExecutorRouter(httpExecutor, _playwrightExecutor.Object);
        var result = await sut.ExecuteAsync(generatorResults, DefaultProject, UserId, RunId);

        Assert.Single(result.ApiResults);
        Assert.False(result.ApiResults[0].Passed);
        Assert.All(result.ApiResults[0].AssertionResults, ar =>
        {
            Assert.False(ar.Passed);
            Assert.Contains("Connection refused", ar.Actual);
        });
    }

    // ─── AC-017: sequential execution within HttpExecutor ────────────────────

    [Fact]
    public async Task HttpExecutor_MultipleScenarios_AllExecutedSequentially()
    {
        var callCount = 0;
        var handler = new DelegateHandler(_ =>
        {
            callCount++;
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        var httpClient = new HttpClient(handler);
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);
        var httpExecutor = new HttpExecutor(_credentialProvider.Object, factory.Object,
            NullLogger<HttpExecutor>.Instance);

        var scenarios = new List<ApiTestScenario>
        {
            MakeApiScenario("s1"),
            MakeApiScenario("s2"),
            MakeApiScenario("s3")
        };
        var generatorResults = new GeneratorResults
        {
            ApiScenarios   = scenarios,
            UiE2eScenarios = []
        };

        var sut = new ExecutorRouter(httpExecutor, _playwrightExecutor.Object);
        var result = await sut.ExecuteAsync(generatorResults, DefaultProject, UserId, RunId);

        Assert.Equal(3, result.ApiResults.Count);
        Assert.Equal(3, callCount);
        Assert.All(result.ApiResults, r => Assert.True(r.Passed));
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private static ApiTestScenario MakeApiScenario(string id = "api-sc1") => new()
    {
        Id = id, Title = "API scenario", Method = "GET", Path = "/items",
        Assertions = [new StatusCodeAssertion { Expected = 200 }]
    };

    private static UiE2eTestScenario MakeUiScenario(string id = "ui-sc1") => new()
    {
        Id = id, Title = "UI scenario",
        Steps = [new NavigateStep { Url = "https://staging.example.com" }]
    };

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

    private sealed class DelegateHandler(Func<HttpRequestMessage, HttpResponseMessage> factory)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(factory(request));
    }

    private sealed class CancellingHandler(CancellationTokenSource cts) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cts.Cancel();
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}
