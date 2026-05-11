using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Testurio.Core.Entities;
using Testurio.Core.Enums;
using Testurio.Core.Interfaces;
using Testurio.Core.Models;
using Testurio.Core.Repositories;
using Testurio.Plugins.ReportWriterPlugin;

// JiraCommentResult is defined in Testurio.Core.Interfaces — bring it into scope for cleaner setup calls.

namespace Testurio.UnitTests.Plugins;

public class ReportWriterPluginTests
{
    private readonly Mock<ITestRunRepository> _testRunRepo = new();
    private readonly Mock<ITestScenarioRepository> _scenarioRepo = new();
    private readonly Mock<IStepResultRepository> _stepResultRepo = new();
    private readonly Mock<IExecutionLogRepository> _executionLogRepo = new();
    private readonly Mock<IProjectRepository> _projectRepo = new();
    private readonly Mock<IJiraApiClient> _jiraApiClient = new();
    private readonly Mock<ISecretResolver> _secretResolver = new();
    private readonly ReportBuilderService _reportBuilder = new();

    public ReportWriterPluginTests()
    {
        // Default: no log entries — tests that don't care about logs stay unaffected.
        _executionLogRepo
            .Setup(r => r.GetByRunAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ExecutionLogEntry>());
    }

    private ReportWriterPlugin CreateSut() => new(
        _testRunRepo.Object,
        _scenarioRepo.Object,
        _stepResultRepo.Object,
        _executionLogRepo.Object,
        _projectRepo.Object,
        _jiraApiClient.Object,
        _secretResolver.Object,
        _reportBuilder,
        NullLogger<ReportWriterPlugin>.Instance);

    private static TestRun MakeRun(TestRunStatus status = TestRunStatus.Completed) => new()
    {
        Id = "run1",
        ProjectId = "proj1",
        UserId = "user1",
        JiraIssueKey = "PROJ-1",
        JiraIssueId = "10001",
        Status = status,
        CompletedAt = DateTimeOffset.UtcNow
    };

    private static Project MakeProject() => new()
    {
        Id = "proj1",
        UserId = "user1",
        Name = "Test Project",
        ProductUrl = "https://app.example.com",
        TestingStrategy = "API and UI tests",
        JiraBaseUrl = "https://example.atlassian.net",
        JiraProjectKey = "PROJ",
        JiraEmail = "qa@example.com",
        JiraApiTokenSecretRef = "secret-ref",
        JiraWebhookSecretRef = "webhook-secret",
        InTestingStatusLabel = "In Testing"
    };

    private static TestScenario MakeScenario() => new()
    {
        Id = "s1",
        TestRunId = "run1",
        ProjectId = "proj1",
        UserId = "user1",
        Title = "Login flow",
        Steps = new List<TestScenarioStep>()
    };

    private static StepResult MakeStep(StepStatus status = StepStatus.Passed) => new()
    {
        TestRunId = "run1",
        ScenarioId = "s1",
        ProjectId = "proj1",
        UserId = "user1",
        StepTitle = "POST /api/login",
        Status = status,
        ExpectedStatusCode = 200,
        ActualStatusCode = 200,
        DurationMs = 100
    };

    [Fact]
    public async Task DeliverAsync_SuccessfulDelivery_ReturnsSuccess()
    {
        var run = MakeRun();
        var project = MakeProject();
        _testRunRepo.Setup(r => r.GetByIdAsync("proj1", "run1", default)).ReturnsAsync(run);
        _projectRepo.Setup(r => r.GetByProjectIdAsync("proj1", default)).ReturnsAsync(project);
        _scenarioRepo.Setup(r => r.GetByRunAsync("proj1", "run1", default)).ReturnsAsync(new[] { MakeScenario() });
        _stepResultRepo.Setup(r => r.GetByRunAsync("proj1", "run1", default)).ReturnsAsync(new[] { MakeStep() });
        _secretResolver.Setup(r => r.ResolveAsync("secret-ref", default)).ReturnsAsync("api-token");
        _jiraApiClient.Setup(c => c.PostCommentAsync(
            "https://example.atlassian.net", "PROJ-1", "qa@example.com", "api-token",
            It.IsAny<string>(), default)).ReturnsAsync(JiraCommentResult.Success());

        var sut = CreateSut();
        var result = await sut.DeliverAsync("proj1", "run1");

        Assert.True(result.IsSuccess);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public async Task DeliverAsync_Jira404_ReturnsFailure()
    {
        var run = MakeRun();
        var project = MakeProject();
        _testRunRepo.Setup(r => r.GetByIdAsync("proj1", "run1", default)).ReturnsAsync(run);
        _projectRepo.Setup(r => r.GetByProjectIdAsync("proj1", default)).ReturnsAsync(project);
        _scenarioRepo.Setup(r => r.GetByRunAsync("proj1", "run1", default)).ReturnsAsync(Array.Empty<TestScenario>());
        _stepResultRepo.Setup(r => r.GetByRunAsync("proj1", "run1", default)).ReturnsAsync(Array.Empty<StepResult>());
        _secretResolver.Setup(r => r.ResolveAsync("secret-ref", default)).ReturnsAsync("api-token");
        // Simulate Jira returning non-2xx — e.g. 404
        _jiraApiClient.Setup(c => c.PostCommentAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), default)).ReturnsAsync(JiraCommentResult.Failure(404, "Issue does not exist"));

        var sut = CreateSut();
        var result = await sut.DeliverAsync("proj1", "run1");

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("PROJ-1", result.ErrorMessage);
    }

    [Fact]
    public async Task DeliverAsync_JiraAuthError_ReturnsFailure()
    {
        var run = MakeRun();
        var project = MakeProject();
        _testRunRepo.Setup(r => r.GetByIdAsync("proj1", "run1", default)).ReturnsAsync(run);
        _projectRepo.Setup(r => r.GetByProjectIdAsync("proj1", default)).ReturnsAsync(project);
        _scenarioRepo.Setup(r => r.GetByRunAsync("proj1", "run1", default)).ReturnsAsync(Array.Empty<TestScenario>());
        _stepResultRepo.Setup(r => r.GetByRunAsync("proj1", "run1", default)).ReturnsAsync(Array.Empty<StepResult>());
        _secretResolver.Setup(r => r.ResolveAsync("secret-ref", default)).ReturnsAsync("bad-token");
        // Simulate auth failure (401 from PostCommentAsync)
        _jiraApiClient.Setup(c => c.PostCommentAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), "bad-token",
            It.IsAny<string>(), default)).ReturnsAsync(JiraCommentResult.Failure(401, "Unauthorized"));

        var sut = CreateSut();
        var result = await sut.DeliverAsync("proj1", "run1");

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public async Task DeliverAsync_RunNotFound_ReturnsFailure()
    {
        _testRunRepo.Setup(r => r.GetByIdAsync("proj1", "run1", default)).ReturnsAsync((TestRun?)null);

        var sut = CreateSut();
        var result = await sut.DeliverAsync("proj1", "run1");

        Assert.False(result.IsSuccess);
        Assert.Contains("run1", result.ErrorMessage);
        _jiraApiClient.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task DeliverAsync_SecretResolutionFails_ReturnsFailure()
    {
        var run = MakeRun();
        var project = MakeProject();
        _testRunRepo.Setup(r => r.GetByIdAsync("proj1", "run1", default)).ReturnsAsync(run);
        _projectRepo.Setup(r => r.GetByProjectIdAsync("proj1", default)).ReturnsAsync(project);
        _scenarioRepo.Setup(r => r.GetByRunAsync("proj1", "run1", default)).ReturnsAsync(Array.Empty<TestScenario>());
        _stepResultRepo.Setup(r => r.GetByRunAsync("proj1", "run1", default)).ReturnsAsync(Array.Empty<StepResult>());
        _secretResolver.Setup(r => r.ResolveAsync("secret-ref", default))
            .ThrowsAsync(new InvalidOperationException("Key Vault unavailable"));

        var sut = CreateSut();
        var result = await sut.DeliverAsync("proj1", "run1");

        Assert.False(result.IsSuccess);
        Assert.Contains("Key Vault unavailable", result.ErrorMessage);
        _jiraApiClient.VerifyNoOtherCalls();
    }
}
