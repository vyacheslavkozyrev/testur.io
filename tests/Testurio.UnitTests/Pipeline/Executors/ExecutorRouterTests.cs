using Moq;
using Testurio.Core.Entities;
using Testurio.Core.Exceptions;
using Testurio.Core.Interfaces;
using Testurio.Core.Models;
using Testurio.Pipeline.Executors;

namespace Testurio.UnitTests.Pipeline.Executors;

/// <summary>
/// Unit tests for <see cref="ExecutorRouter"/> covering all routing decisions
/// defined in feature 0029 acceptance criteria (AC-001 through AC-006).
/// Both executors are mocked — no real HTTP calls or browser instances.
/// </summary>
public class ExecutorRouterTests
{
    private readonly Mock<IHttpExecutor> _httpExecutor = new();
    private readonly Mock<IPlaywrightExecutor> _playwrightExecutor = new();

    private static readonly Project DefaultProject = new()
    {
        UserId = "user1",
        Name = "Test",
        ProductUrl = "https://api.example.com",
        TestingStrategy = "both"
    };

    private static readonly Guid UserId  = Guid.NewGuid();
    private static readonly Guid RunId   = Guid.NewGuid();

    private ExecutorRouter CreateSut() =>
        new(_httpExecutor.Object, _playwrightExecutor.Object);

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

    private static ApiScenarioResult MakeApiResult(string id = "api-sc1") => new()
    {
        ScenarioId = id, Title = "API scenario", Passed = true, DurationMs = 100,
        AssertionResults = []
    };

    private static UiE2eScenarioResult MakeUiResult(string id = "ui-sc1") => new()
    {
        ScenarioId = id, Title = "UI scenario", Passed = true, DurationMs = 500,
        StepResults = []
    };

    // ─── AC-001: both lists non-empty → both executors invoked in parallel ────

    [Fact]
    public async Task BothListsNonEmpty_InvokesHttpAndPlaywrightExecutors()
    {
        var apiResults = new List<ApiScenarioResult> { MakeApiResult() };
        var uiResults  = new List<UiE2eScenarioResult> { MakeUiResult() };

        _httpExecutor
            .Setup(e => e.ExecuteAsync(It.IsAny<IReadOnlyList<ApiTestScenario>>(),
                It.IsAny<Project>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(apiResults);

        _playwrightExecutor
            .Setup(e => e.ExecuteAsync(It.IsAny<IReadOnlyList<UiE2eTestScenario>>(),
                It.IsAny<Project>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(uiResults);

        var generatorResults = new GeneratorResults
        {
            ApiScenarios    = [MakeApiScenario()],
            UiE2eScenarios  = [MakeUiScenario()]
        };

        var sut = CreateSut();
        var result = await sut.ExecuteAsync(generatorResults, DefaultProject, UserId, RunId);

        Assert.Single(result.ApiResults);
        Assert.Single(result.UiE2eResults);

        _httpExecutor.Verify(e => e.ExecuteAsync(
            It.IsAny<IReadOnlyList<ApiTestScenario>>(),
            DefaultProject, It.IsAny<CancellationToken>()), Times.Once);

        _playwrightExecutor.Verify(e => e.ExecuteAsync(
            It.IsAny<IReadOnlyList<UiE2eTestScenario>>(),
            DefaultProject, UserId, RunId, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ─── AC-002: only API list non-empty → only HttpExecutor invoked ──────────

    [Fact]
    public async Task OnlyApiScenariosNonEmpty_InvokesOnlyHttpExecutor()
    {
        var apiResults = new List<ApiScenarioResult> { MakeApiResult() };

        _httpExecutor
            .Setup(e => e.ExecuteAsync(It.IsAny<IReadOnlyList<ApiTestScenario>>(),
                It.IsAny<Project>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(apiResults);

        var generatorResults = new GeneratorResults
        {
            ApiScenarios    = [MakeApiScenario()],
            UiE2eScenarios  = []
        };

        var sut = CreateSut();
        var result = await sut.ExecuteAsync(generatorResults, DefaultProject, UserId, RunId);

        Assert.Single(result.ApiResults);
        Assert.Empty(result.UiE2eResults);

        _httpExecutor.Verify(e => e.ExecuteAsync(
            It.IsAny<IReadOnlyList<ApiTestScenario>>(),
            DefaultProject, It.IsAny<CancellationToken>()), Times.Once);

        _playwrightExecutor.Verify(e => e.ExecuteAsync(
            It.IsAny<IReadOnlyList<UiE2eTestScenario>>(),
            It.IsAny<Project>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ─── AC-003: only UI list non-empty → only PlaywrightExecutor invoked ─────

    [Fact]
    public async Task OnlyUiScenariosNonEmpty_InvokesOnlyPlaywrightExecutor()
    {
        var uiResults = new List<UiE2eScenarioResult> { MakeUiResult() };

        _playwrightExecutor
            .Setup(e => e.ExecuteAsync(It.IsAny<IReadOnlyList<UiE2eTestScenario>>(),
                It.IsAny<Project>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(uiResults);

        var generatorResults = new GeneratorResults
        {
            ApiScenarios    = [],
            UiE2eScenarios  = [MakeUiScenario()]
        };

        var sut = CreateSut();
        var result = await sut.ExecuteAsync(generatorResults, DefaultProject, UserId, RunId);

        Assert.Empty(result.ApiResults);
        Assert.Single(result.UiE2eResults);

        _httpExecutor.Verify(e => e.ExecuteAsync(
            It.IsAny<IReadOnlyList<ApiTestScenario>>(),
            It.IsAny<Project>(), It.IsAny<CancellationToken>()), Times.Never);

        _playwrightExecutor.Verify(e => e.ExecuteAsync(
            It.IsAny<IReadOnlyList<UiE2eTestScenario>>(),
            DefaultProject, UserId, RunId, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ─── AC-004: both lists empty → ExecutorRouterException thrown ────────────

    [Fact]
    public async Task BothListsEmpty_ThrowsExecutorRouterException()
    {
        var generatorResults = new GeneratorResults
        {
            ApiScenarios    = [],
            UiE2eScenarios  = []
        };

        var sut = CreateSut();

        var ex = await Assert.ThrowsAsync<ExecutorRouterException>(
            () => sut.ExecuteAsync(generatorResults, DefaultProject, UserId, RunId));

        Assert.Equal(
            "No scenarios to execute — both API and UI E2E scenario lists are empty",
            ex.Message);

        _httpExecutor.Verify(e => e.ExecuteAsync(
            It.IsAny<IReadOnlyList<ApiTestScenario>>(),
            It.IsAny<Project>(), It.IsAny<CancellationToken>()), Times.Never);

        _playwrightExecutor.Verify(e => e.ExecuteAsync(
            It.IsAny<IReadOnlyList<UiE2eTestScenario>>(),
            It.IsAny<Project>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ─── AC-005: CancellationToken forwarded to executors ─────────────────────

    [Fact]
    public async Task CancellationToken_ForwardedToBothExecutors()
    {
        using var cts = new CancellationTokenSource();
        var token = cts.Token;

        _httpExecutor
            .Setup(e => e.ExecuteAsync(It.IsAny<IReadOnlyList<ApiTestScenario>>(),
                It.IsAny<Project>(), token))
            .ReturnsAsync([MakeApiResult()]);

        _playwrightExecutor
            .Setup(e => e.ExecuteAsync(It.IsAny<IReadOnlyList<UiE2eTestScenario>>(),
                It.IsAny<Project>(), It.IsAny<Guid>(), It.IsAny<Guid>(), token))
            .ReturnsAsync([MakeUiResult()]);

        var generatorResults = new GeneratorResults
        {
            ApiScenarios    = [MakeApiScenario()],
            UiE2eScenarios  = [MakeUiScenario()]
        };

        var sut = CreateSut();
        await sut.ExecuteAsync(generatorResults, DefaultProject, UserId, RunId, token);

        _httpExecutor.Verify(e => e.ExecuteAsync(
            It.IsAny<IReadOnlyList<ApiTestScenario>>(),
            It.IsAny<Project>(), token), Times.Once);

        _playwrightExecutor.Verify(e => e.ExecuteAsync(
            It.IsAny<IReadOnlyList<UiE2eTestScenario>>(),
            It.IsAny<Project>(), It.IsAny<Guid>(), It.IsAny<Guid>(), token), Times.Once);
    }

    // ─── AC-006: ExecutionResult merges ApiResults and UiE2eResults ──────────

    [Fact]
    public async Task ExecutionResult_MergesResultsFromBothExecutors()
    {
        var apiRes1 = MakeApiResult("api-1");
        var apiRes2 = MakeApiResult("api-2");
        var uiRes1  = MakeUiResult("ui-1");

        _httpExecutor
            .Setup(e => e.ExecuteAsync(It.IsAny<IReadOnlyList<ApiTestScenario>>(),
                It.IsAny<Project>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([apiRes1, apiRes2]);

        _playwrightExecutor
            .Setup(e => e.ExecuteAsync(It.IsAny<IReadOnlyList<UiE2eTestScenario>>(),
                It.IsAny<Project>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([uiRes1]);

        var generatorResults = new GeneratorResults
        {
            ApiScenarios    = [MakeApiScenario("api-1"), MakeApiScenario("api-2")],
            UiE2eScenarios  = [MakeUiScenario("ui-1")]
        };

        var sut = CreateSut();
        var result = await sut.ExecuteAsync(generatorResults, DefaultProject, UserId, RunId);

        Assert.Equal(2, result.ApiResults.Count);
        Assert.Single(result.UiE2eResults);
        Assert.Equal("api-1", result.ApiResults[0].ScenarioId);
        Assert.Equal("api-2", result.ApiResults[1].ScenarioId);
        Assert.Equal("ui-1",  result.UiE2eResults[0].ScenarioId);
    }
}
