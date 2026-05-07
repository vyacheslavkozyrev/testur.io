using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Testurio.Core.Entities;
using Testurio.Core.Enums;
using Testurio.Core.Interfaces;
using Testurio.Core.Models;
using Testurio.Core.Repositories;
using Testurio.Infrastructure.KeyVault;
using Testurio.Plugins.TestExecutorPlugin;

namespace Testurio.UnitTests.Steps;

public class ApiTestExecutionStepTests
{
    private readonly Mock<TestExecutorPlugin> _executorPlugin = new(
        new System.Net.Http.HttpClient(),
        new ResponseSchemaValidator(),
        NullLogger<TestExecutorPlugin>.Instance);

    private readonly Mock<ISecretResolver> _secretResolver = new();
    private readonly Mock<IStepResultRepository> _stepResultRepository = new();
    private readonly Mock<ITestRunRepository> _testRunRepository = new();


    private ApiTestExecutionStep CreateSut() => new(
        _executorPlugin.Object,
        new KeyVaultCredentialClient(_secretResolver.Object),
        _stepResultRepository.Object,
        _testRunRepository.Object,
        NullLogger<ApiTestExecutionStep>.Instance);

    private static Project BuildProject(string? bearerTokenRef = null) => new()
    {
        Id = "project-1",
        UserId = "user-1",
        Name = "Test Project",
        ProductUrl = "https://api.example.com",
        JiraBaseUrl = "https://jira.example.com",
        JiraProjectKey = "PROJ",
        JiraEmail = "user@example.com",
        JiraApiTokenSecretRef = "ref://jira-token",
        JiraWebhookSecretRef = "ref://webhook-secret",
        InTestingStatusLabel = "In Testing",
        BearerTokenSecretRef = bearerTokenRef
    };

    private static TestRun BuildTestRun() => new()
    {
        ProjectId = "project-1",
        UserId = "user-1",
        JiraIssueKey = "PROJ-1",
        JiraIssueId = "1",
        Status = TestRunStatus.Active
    };

    private static TestScenario BuildScenario(string id = "scenario-1") => new()
    {
        Id = id,
        TestRunId = "run-1",
        ProjectId = "project-1",
        UserId = "user-1",
        Title = "Scenario Title",
        Steps = [new TestScenarioStep { Order = 1, Description = "GET /api/health", ExpectedResult = "HTTP 200" }]
    };

    private static StepResult BuildPassedResult(string scenarioId = "scenario-1") => new()
    {
        ProjectId = "project-1",
        TestRunId = "run-1",
        ScenarioId = scenarioId,
        StepTitle = "GET /api/health",
        Status = StepStatus.Passed,
        DurationMs = 50
    };

    private static StepResult BuildFailedResult(string scenarioId = "scenario-1") => new()
    {
        ProjectId = "project-1",
        TestRunId = "run-1",
        ScenarioId = scenarioId,
        StepTitle = "GET /api/health",
        Status = StepStatus.Failed,
        DurationMs = 50,
        FailureMessage = "Expected 200 but got 500"
    };

    // --- Run status aggregation (AC-017) ---

    [Fact]
    public async Task ExecuteAsync_AllStepsPass_RunStatusSetToCompleted()
    {
        var testRun = BuildTestRun();
        var project = BuildProject();
        var scenarios = new List<TestScenario> { BuildScenario() };

        _executorPlugin
            .Setup(p => p.ExecuteScenarioAsync(
                It.IsAny<TestScenario>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<StepResult> { BuildPassedResult() });

        _stepResultRepository.Setup(r => r.CreateBatchAsync(It.IsAny<IEnumerable<StepResult>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _testRunRepository.Setup(r => r.UpdateAsync(It.IsAny<TestRun>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TestRun run, CancellationToken _) => run);

        var sut = CreateSut();
        await sut.ExecuteAsync(testRun, project, scenarios);

        Assert.Equal(TestRunStatus.Completed, testRun.Status);
    }

    [Fact]
    public async Task ExecuteAsync_AnyStepFails_RunStatusSetToFailed()
    {
        var testRun = BuildTestRun();
        var project = BuildProject();
        var scenarios = new List<TestScenario> { BuildScenario("s1"), BuildScenario("s2") };

        _executorPlugin
            .SetupSequence(p => p.ExecuteScenarioAsync(
                It.IsAny<TestScenario>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<StepResult> { BuildPassedResult("s1") })
            .ReturnsAsync(new List<StepResult> { BuildFailedResult("s2") });

        _stepResultRepository.Setup(r => r.CreateBatchAsync(It.IsAny<IEnumerable<StepResult>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _testRunRepository.Setup(r => r.UpdateAsync(It.IsAny<TestRun>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TestRun run, CancellationToken _) => run);

        var sut = CreateSut();
        await sut.ExecuteAsync(testRun, project, scenarios);

        Assert.Equal(TestRunStatus.Failed, testRun.Status);
    }

    // --- All scenarios executed regardless of individual outcomes (AC-004) ---

    [Fact]
    public async Task ExecuteAsync_MultipleScenarios_AllScenariosExecuted()
    {
        var testRun = BuildTestRun();
        var project = BuildProject();
        var scenarios = new List<TestScenario>
        {
            BuildScenario("s1"),
            BuildScenario("s2"),
            BuildScenario("s3")
        };

        _executorPlugin
            .Setup(p => p.ExecuteScenarioAsync(
                It.IsAny<TestScenario>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<StepResult> { BuildPassedResult() });

        _stepResultRepository.Setup(r => r.CreateBatchAsync(It.IsAny<IEnumerable<StepResult>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _testRunRepository.Setup(r => r.UpdateAsync(It.IsAny<TestRun>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TestRun run, CancellationToken _) => run);

        var sut = CreateSut();
        await sut.ExecuteAsync(testRun, project, scenarios);

        _executorPlugin.Verify(
            p => p.ExecuteScenarioAsync(
                It.IsAny<TestScenario>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Exactly(3));
    }

    // --- Step results persisted (AC-016) ---

    [Fact]
    public async Task ExecuteAsync_AllResults_PersistedViaBatchCreate()
    {
        var testRun = BuildTestRun();
        var project = BuildProject();
        var scenarios = new List<TestScenario> { BuildScenario("s1"), BuildScenario("s2") };

        _executorPlugin
            .Setup(p => p.ExecuteScenarioAsync(
                It.IsAny<TestScenario>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<StepResult> { BuildPassedResult() });

        IEnumerable<StepResult>? capturedResults = null;
        _stepResultRepository
            .Setup(r => r.CreateBatchAsync(It.IsAny<IEnumerable<StepResult>>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<StepResult>, CancellationToken>((results, _) => capturedResults = results)
            .Returns(Task.CompletedTask);
        _testRunRepository.Setup(r => r.UpdateAsync(It.IsAny<TestRun>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TestRun run, CancellationToken _) => run);

        var sut = CreateSut();
        await sut.ExecuteAsync(testRun, project, scenarios);

        Assert.NotNull(capturedResults);
        Assert.Equal(2, capturedResults!.Count()); // 1 result per scenario
        _stepResultRepository.Verify(
            r => r.CreateBatchAsync(It.IsAny<IEnumerable<StepResult>>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // --- No Bearer token configured (AC-007) ---

    [Fact]
    public async Task ExecuteAsync_NoBearerTokenSecretRef_NullTokenPassedToPlugin()
    {
        var testRun = BuildTestRun();
        var project = BuildProject(bearerTokenRef: null);
        var scenarios = new List<TestScenario> { BuildScenario() };

        string? capturedToken = "not-null";
        _executorPlugin
            .Setup(p => p.ExecuteScenarioAsync(
                It.IsAny<TestScenario>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .Callback<TestScenario, string, string?, CancellationToken>((_, _, token, _) => capturedToken = token)
            .ReturnsAsync(new List<StepResult> { BuildPassedResult() });

        _stepResultRepository.Setup(r => r.CreateBatchAsync(It.IsAny<IEnumerable<StepResult>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _testRunRepository.Setup(r => r.UpdateAsync(It.IsAny<TestRun>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TestRun run, CancellationToken _) => run);

        var sut = CreateSut();
        await sut.ExecuteAsync(testRun, project, scenarios);

        Assert.Null(capturedToken);
    }

    // --- Returns results (AC-018) ---

    [Fact]
    public async Task ExecuteAsync_ReturnsAllStepResults()
    {
        var testRun = BuildTestRun();
        var project = BuildProject();
        var scenarios = new List<TestScenario> { BuildScenario() };
        var expectedResult = BuildPassedResult();

        _executorPlugin
            .Setup(p => p.ExecuteScenarioAsync(
                It.IsAny<TestScenario>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<StepResult> { expectedResult });

        _stepResultRepository.Setup(r => r.CreateBatchAsync(It.IsAny<IEnumerable<StepResult>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _testRunRepository.Setup(r => r.UpdateAsync(It.IsAny<TestRun>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TestRun run, CancellationToken _) => run);

        var sut = CreateSut();
        var results = await sut.ExecuteAsync(testRun, project, scenarios);

        Assert.Single(results);
        Assert.Same(expectedResult, results[0]);
    }
}
