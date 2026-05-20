using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Testurio.Core.Entities;
using Testurio.Core.Enums;
using Testurio.Core.Interfaces;
using Testurio.Core.Models;
using Testurio.Core.Repositories;
using Testurio.Worker.Steps;
using Xunit;

namespace Testurio.UnitTests.Pipeline.Steps;

public class WorkItemTransitionStepTests
{
    private readonly Mock<IWorkItemTransitionService> _transitionService = new();
    private readonly Mock<ITestRunRepository> _testRunRepository = new();

    private WorkItemTransitionStep CreateSut() =>
        new(_transitionService.Object, _testRunRepository.Object,
            NullLogger<WorkItemTransitionStep>.Instance);

    private static TestRun MakeTestRun(string projectId = "proj-1") =>
        new() { ProjectId = projectId, UserId = "user-1", JiraIssueKey = "PROJ-1", JiraIssueId = "1" };

    private static Project AdoProject(
        string? passedStatus = "Closed",
        string? failedStatus = "Active") =>
        new()
        {
            UserId = "user-1",
            Name = "T",
            ProductUrl = "https://x.com",
            TestingStrategy = "BDD",
            PmTool = PMToolType.Ado,
            AdoPassedTransitionStatus = passedStatus,
            AdoFailedTransitionStatus = failedStatus,
        };

    private static Project JiraProject(
        string? passedStatus = "Done",
        string? failedStatus = "Rejected") =>
        new()
        {
            UserId = "user-1",
            Name = "T",
            ProductUrl = "https://x.com",
            TestingStrategy = "BDD",
            PmTool = PMToolType.Jira,
            JiraPassedTransitionStatus = passedStatus,
            JiraFailedTransitionStatus = failedStatus,
        };

    private static ExecutionResult AllPassed() => new()
    {
        ApiResults = new[]
        {
            new ApiScenarioResult
            {
                ScenarioId = "s1", Title = "T1", Passed = true, DurationMs = 100,
                AssertionResults = Array.Empty<AssertionResult>(),
            }
        },
        UiE2eResults = Array.Empty<UiE2eScenarioResult>(),
    };

    private static ExecutionResult HasFailed() => new()
    {
        ApiResults = new[]
        {
            new ApiScenarioResult
            {
                ScenarioId = "s1", Title = "T1", Passed = false, DurationMs = 50,
                AssertionResults = Array.Empty<AssertionResult>(),
            }
        },
        UiE2eResults = Array.Empty<UiE2eScenarioResult>(),
    };

    private static ExecutionResult Empty() => new()
    {
        ApiResults = Array.Empty<ApiScenarioResult>(),
        UiE2eResults = Array.Empty<UiE2eScenarioResult>(),
    };

    // ─── Target status selection ──────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_AllPassed_Ado_CallsTransitionWithPassedStatus()
    {
        _transitionService
            .Setup(s => s.TransitionAsync(It.IsAny<Project>(), It.IsAny<TestRun>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WorkItemTransitionResult(StatusTransitionOutcome.Succeeded, "Closed", null));

        var testRun = MakeTestRun();
        var project = AdoProject();
        await CreateSut().ExecuteAsync(testRun, project, AllPassed());

        _transitionService.Verify(s => s.TransitionAsync(
            project, testRun, "Closed", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_HasFailed_Ado_CallsTransitionWithFailedStatus()
    {
        _transitionService
            .Setup(s => s.TransitionAsync(It.IsAny<Project>(), It.IsAny<TestRun>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WorkItemTransitionResult(StatusTransitionOutcome.Succeeded, "Active", null));

        var testRun = MakeTestRun();
        var project = AdoProject();
        await CreateSut().ExecuteAsync(testRun, project, HasFailed());

        _transitionService.Verify(s => s.TransitionAsync(
            project, testRun, "Active", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_AllPassed_Jira_UsesJiraPassedStatus()
    {
        _transitionService
            .Setup(s => s.TransitionAsync(It.IsAny<Project>(), It.IsAny<TestRun>(), "Done", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WorkItemTransitionResult(StatusTransitionOutcome.Succeeded, "Done", null));

        var project = JiraProject();
        await CreateSut().ExecuteAsync(MakeTestRun(), project, AllPassed());

        _transitionService.Verify(s => s.TransitionAsync(
            project, It.IsAny<TestRun>(), "Done", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_HasFailed_Jira_UsesJiraFailedStatus()
    {
        _transitionService
            .Setup(s => s.TransitionAsync(It.IsAny<Project>(), It.IsAny<TestRun>(), "Rejected", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WorkItemTransitionResult(StatusTransitionOutcome.Succeeded, "Rejected", null));

        var project = JiraProject();
        await CreateSut().ExecuteAsync(MakeTestRun(), project, HasFailed());

        _transitionService.Verify(s => s.TransitionAsync(
            project, It.IsAny<TestRun>(), "Rejected", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_Empty_TreatedAsPassed_UsesPassedStatus()
    {
        // Empty execution (no scenarios) — all() vacuously true → treat as passed.
        _transitionService
            .Setup(s => s.TransitionAsync(It.IsAny<Project>(), It.IsAny<TestRun>(), "Closed", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WorkItemTransitionResult(StatusTransitionOutcome.NotConfigured, null, null));

        var project = AdoProject();
        await CreateSut().ExecuteAsync(MakeTestRun(), project, Empty());

        _transitionService.Verify(s => s.TransitionAsync(
            project, It.IsAny<TestRun>(), "Closed", It.IsAny<CancellationToken>()), Times.Once);
    }

    // ─── TestRun field updates ────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_OnSuccess_SetsTransitionFieldsOnTestRun()
    {
        _transitionService
            .Setup(s => s.TransitionAsync(It.IsAny<Project>(), It.IsAny<TestRun>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WorkItemTransitionResult(StatusTransitionOutcome.Succeeded, "Closed", null));

        var testRun = MakeTestRun();
        await CreateSut().ExecuteAsync(testRun, AdoProject(), AllPassed());

        Assert.Equal(StatusTransitionOutcome.Succeeded, testRun.StatusTransitionOutcome);
        Assert.Equal("Closed", testRun.StatusTransitionedTo);
        Assert.Null(testRun.StatusTransitionError);
    }

    [Fact]
    public async Task ExecuteAsync_OnFailure_SetsErrorFieldOnTestRun()
    {
        _transitionService
            .Setup(s => s.TransitionAsync(It.IsAny<Project>(), It.IsAny<TestRun>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WorkItemTransitionResult(StatusTransitionOutcome.Failed, null, "HTTP 404: Not found"));

        var testRun = MakeTestRun();
        await CreateSut().ExecuteAsync(testRun, AdoProject(), AllPassed());

        Assert.Equal(StatusTransitionOutcome.Failed, testRun.StatusTransitionOutcome);
        Assert.Null(testRun.StatusTransitionedTo);
        Assert.Equal("HTTP 404: Not found", testRun.StatusTransitionError);
    }

    [Fact]
    public async Task ExecuteAsync_OnNotConfigured_SetsNotConfiguredOutcome()
    {
        _transitionService
            .Setup(s => s.TransitionAsync(It.IsAny<Project>(), It.IsAny<TestRun>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WorkItemTransitionResult(StatusTransitionOutcome.NotConfigured, null, null));

        var testRun = MakeTestRun();
        await CreateSut().ExecuteAsync(testRun, AdoProject(passedStatus: null), AllPassed());

        Assert.Equal(StatusTransitionOutcome.NotConfigured, testRun.StatusTransitionOutcome);
        Assert.Null(testRun.StatusTransitionedTo);
    }

    // ─── Persistence ──────────────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_AlwaysPersistsTestRunAfterTransition()
    {
        _transitionService
            .Setup(s => s.TransitionAsync(It.IsAny<Project>(), It.IsAny<TestRun>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WorkItemTransitionResult(StatusTransitionOutcome.Succeeded, "Closed", null));

        var testRun = MakeTestRun();
        _testRunRepository
            .Setup(r => r.UpdateAsync(It.IsAny<TestRun>(), CancellationToken.None))
            .ReturnsAsync(testRun);

        await CreateSut().ExecuteAsync(testRun, AdoProject(), AllPassed());

        _testRunRepository.Verify(r => r.UpdateAsync(testRun, CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenRepositoryThrows_DoesNotPropagate()
    {
        _transitionService
            .Setup(s => s.TransitionAsync(It.IsAny<Project>(), It.IsAny<TestRun>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WorkItemTransitionResult(StatusTransitionOutcome.Succeeded, "Closed", null));

        _testRunRepository
            .Setup(r => r.UpdateAsync(It.IsAny<TestRun>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Cosmos unavailable"));

        // Should not throw.
        await CreateSut().ExecuteAsync(MakeTestRun(), AdoProject(), AllPassed());
    }
}
