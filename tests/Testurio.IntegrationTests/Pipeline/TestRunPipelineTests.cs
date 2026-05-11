using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Testurio.Core.Entities;
using Testurio.Core.Enums;
using Testurio.Core.Interfaces;
using Testurio.Core.Models;
using Testurio.Core.Repositories;
using Testurio.Plugins.ReportWriterPlugin;
using Testurio.Worker.Steps;

// JiraCommentResult is defined in Testurio.Core.Interfaces — imported for mock setup.

namespace Testurio.IntegrationTests.Pipeline;

/// <summary>
/// Integration tests for the test run pipeline covering the report delivery stage.
/// Feature 0002 (ScenarioGenerationStep) and feature 0003 (ApiTestExecutionStep)
/// will extend this fixture when those features are implemented.
/// </summary>
public class TestRunPipelineTests
{
    private readonly Mock<ITestRunRepository> _testRunRepo = new();
    private readonly Mock<ITestScenarioRepository> _scenarioRepo = new();
    private readonly Mock<IStepResultRepository> _stepResultRepo = new();
    private readonly Mock<IProjectRepository> _projectRepo = new();
    private readonly Mock<IJiraApiClient> _jiraApiClient = new();
    private readonly Mock<ISecretResolver> _secretResolver = new();

    private ReportDeliveryStep CreateReportDeliveryStep()
    {
        var builder = new ReportBuilderService();
        var plugin = new ReportWriterPlugin(
            _testRunRepo.Object,
            _scenarioRepo.Object,
            _stepResultRepo.Object,
            _projectRepo.Object,
            _jiraApiClient.Object,
            _secretResolver.Object,
            builder,
            NullLogger<ReportWriterPlugin>.Instance);
        return new ReportDeliveryStep(plugin, _testRunRepo.Object, NullLogger<ReportDeliveryStep>.Instance);
    }

    private static TestRun MakeRun(TestRunStatus status = TestRunStatus.Active) => new()
    {
        Id = "run1",
        ProjectId = "proj1",
        UserId = "user1",
        JiraIssueKey = "PROJ-1",
        JiraIssueId = "10001",
        Status = status
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
        Title = "Add item to cart",
        Steps = new List<TestScenarioStep>()
    };

    private static StepResult MakeStep(StepStatus status = StepStatus.Passed) => new()
    {
        TestRunId = "run1",
        ScenarioId = "s1",
        ProjectId = "proj1",
        UserId = "user1",
        StepTitle = "POST /api/cart",
        Status = status,
        ExpectedStatusCode = 200,
        ActualStatusCode = 200,
        DurationMs = 80
    };

    [Fact]
    public async Task Pipeline_SuccessfulRun_SetsStatusToCompletedAndDeliversReport()
    {
        // Arrange: full pipeline pass — all scenarios and steps pass, Jira delivery succeeds
        var run = MakeRun(TestRunStatus.Completed);
        var project = MakeProject();
        var scenario = MakeScenario();
        var step = MakeStep(StepStatus.Passed);

        _testRunRepo.Setup(r => r.GetByIdAsync("proj1", "run1", default)).ReturnsAsync(run);
        _projectRepo.Setup(r => r.GetByProjectIdAsync("proj1", default)).ReturnsAsync(project);
        _scenarioRepo.Setup(r => r.GetByRunAsync("proj1", "run1", default)).ReturnsAsync(new[] { scenario });
        _stepResultRepo.Setup(r => r.GetByRunAsync("proj1", "run1", default)).ReturnsAsync(new[] { step });
        _secretResolver.Setup(r => r.ResolveAsync("secret-ref", default)).ReturnsAsync("api-token");
        _jiraApiClient.Setup(c => c.PostCommentAsync(
            It.IsAny<string>(), "PROJ-1", It.IsAny<string>(), It.IsAny<string>(),
            It.Is<string>(s => s.Contains("Passed")), default)).ReturnsAsync(JiraCommentResult.Success());

        TestRun? updatedRun = null;
        _testRunRepo.Setup(r => r.UpdateAsync(It.IsAny<TestRun>(), It.IsAny<CancellationToken>()))
            .Callback<TestRun, CancellationToken>((r, _) => updatedRun = r)
            .ReturnsAsync((TestRun r, CancellationToken _) => r);

        // Act
        var step1 = CreateReportDeliveryStep();
        await step1.ExecuteAsync(run);

        // Assert
        Assert.NotNull(updatedRun);
        Assert.Equal(TestRunStatus.Completed, updatedRun!.Status);
        Assert.NotNull(updatedRun.CompletedAt);
        _jiraApiClient.Verify(c => c.PostCommentAsync(
            It.IsAny<string>(), "PROJ-1", It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), default), Times.Once);
    }

    [Fact]
    public async Task Pipeline_JiraDeliveryFails_SetsStatusToReportDeliveryFailed()
    {
        // Arrange: execution succeeds but Jira comment post fails
        var run = MakeRun(TestRunStatus.Completed);
        var project = MakeProject();

        _testRunRepo.Setup(r => r.GetByIdAsync("proj1", "run1", default)).ReturnsAsync(run);
        _projectRepo.Setup(r => r.GetByProjectIdAsync("proj1", default)).ReturnsAsync(project);
        _scenarioRepo.Setup(r => r.GetByRunAsync("proj1", "run1", default)).ReturnsAsync(Array.Empty<TestScenario>());
        _stepResultRepo.Setup(r => r.GetByRunAsync("proj1", "run1", default)).ReturnsAsync(Array.Empty<StepResult>());
        _secretResolver.Setup(r => r.ResolveAsync("secret-ref", default)).ReturnsAsync("api-token");
        _jiraApiClient.Setup(c => c.PostCommentAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(JiraCommentResult.Failure(503, "Service Unavailable"));

        TestRun? updatedRun = null;
        _testRunRepo.Setup(r => r.UpdateAsync(It.IsAny<TestRun>(), It.IsAny<CancellationToken>()))
            .Callback<TestRun, CancellationToken>((r, _) => updatedRun = r)
            .ReturnsAsync((TestRun r, CancellationToken _) => r);

        // Act
        var step = CreateReportDeliveryStep();
        await step.ExecuteAsync(run);

        // Assert
        Assert.NotNull(updatedRun);
        Assert.Equal(TestRunStatus.ReportDeliveryFailed, updatedRun!.Status);
        Assert.NotNull(updatedRun.DeliveryError);
        // DeliveryError must include the HTTP status code for run-history diagnostics (AC-014).
        Assert.Contains("503", updatedRun.DeliveryError);
        Assert.NotNull(updatedRun.CompletedAt);
    }

    [Fact]
    public async Task Pipeline_FailedRun_ReportContainsFailuresSection()
    {
        // Arrange: a run with a failed step — report should include a Failures section
        var run = MakeRun(TestRunStatus.Failed);
        var project = MakeProject();
        var scenario = MakeScenario();
        var failedStep = MakeStep(StepStatus.Failed);
        failedStep = new StepResult
        {
            TestRunId = failedStep.TestRunId,
            ScenarioId = failedStep.ScenarioId,
            ProjectId = failedStep.ProjectId,
            UserId = failedStep.UserId,
            StepTitle = failedStep.StepTitle,
            Status = StepStatus.Failed,
            ExpectedStatusCode = 200,
            ActualStatusCode = 404,
            DurationMs = 50
        };

        string? postedComment = null;
        _testRunRepo.Setup(r => r.GetByIdAsync("proj1", "run1", default)).ReturnsAsync(run);
        _projectRepo.Setup(r => r.GetByProjectIdAsync("proj1", default)).ReturnsAsync(project);
        _scenarioRepo.Setup(r => r.GetByRunAsync("proj1", "run1", default)).ReturnsAsync(new[] { scenario });
        _stepResultRepo.Setup(r => r.GetByRunAsync("proj1", "run1", default)).ReturnsAsync(new[] { failedStep });
        _secretResolver.Setup(r => r.ResolveAsync("secret-ref", default)).ReturnsAsync("api-token");
        _jiraApiClient.Setup(c => c.PostCommentAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), default))
            .Callback<string, string, string, string, string, CancellationToken>((_, _, _, _, body, _) => postedComment = body)
            .ReturnsAsync(JiraCommentResult.Success());
        _testRunRepo.Setup(r => r.UpdateAsync(It.IsAny<TestRun>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TestRun r, CancellationToken _) => r);

        // Act
        var step = CreateReportDeliveryStep();
        await step.ExecuteAsync(run);

        // Assert — the posted comment contains the Failures section
        Assert.NotNull(postedComment);
        Assert.Contains("Failures", postedComment);
        Assert.Contains("Add item to cart", postedComment);
        Assert.Contains("POST /api/cart", postedComment);
    }
}
