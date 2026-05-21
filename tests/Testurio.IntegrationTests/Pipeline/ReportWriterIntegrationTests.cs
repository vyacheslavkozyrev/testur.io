using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Testurio.Core.Entities;
using Testurio.Core.Enums;
using Testurio.Core.Exceptions;
using Testurio.Core.Interfaces;
using Testurio.Core.Models;
using Testurio.Core.Repositories;
using Testurio.Pipeline.ReportWriter;

namespace Testurio.IntegrationTests.Pipeline;

/// <summary>
/// Integration tests for stage 6 (ReportWriter / feature 0030) via <see cref="ReportWriter"/>.
/// Exercises the full orchestration path through the stage — Claude stub, comment posting,
/// and Cosmos repository mock.
/// T016: covers both executor results present, PM tool unavailable, and ReportWriterException.
/// </summary>
public class ReportWriterIntegrationTests
{
    private readonly Mock<ILlmGenerationClient> _llmClient = new();
    private readonly Mock<IJiraApiClient> _jiraClient = new();
    private readonly Mock<IADOClient> _adoClient = new();
    private readonly Mock<ISecretResolver> _secretResolver = new();
    private readonly Mock<ITestResultRepository> _testResultRepo = new();

    private ReportWriter CreateSut() =>
        new(_llmClient.Object, _jiraClient.Object, _adoClient.Object,
            _secretResolver.Object, _testResultRepo.Object,
            NullLogger<ReportWriter>.Instance);

    // ─── Shared fixtures ──────────────────────────────────────────────────────

    private static ParsedStory DefaultStory => new()
    {
        Title = "User can view dashboard",
        Description = "View dashboard",
        AcceptanceCriteria = ["Dashboard loads"]
    };

    private static Project JiraProject => new()
    {
        UserId = "user1",
        Name = "Proj",
        ProductUrl = "https://staging.example.com",
        TestingStrategy = "both",
        PmTool = PMToolType.Jira,
        JiraBaseUrl = "https://jira.example.com",
        JiraEmailSecretUri = "https://vault/email",
        JiraApiTokenSecretUri = "https://vault/token",
    };

    private static TestRun DefaultRun => new()
    {
        ProjectId = "proj1",
        UserId = "user1",
        JiraIssueKey = "PROJ-42",
        JiraIssueId = "20001",
        ExecutionWarnings = []
    };

    private static ExecutionResult BothExecutorsResult => new()
    {
        ApiResults = [new ApiScenarioResult
        {
            ScenarioId = "api-1", Title = "GET /dashboard", Passed = true,
            DurationMs = 80, AssertionResults = []
        }],
        UiE2eResults = [new UiE2eScenarioResult
        {
            ScenarioId = "ui-1", Title = "Dashboard loads", Passed = true,
            DurationMs = 500, StepResults = []
        }]
    };

    private static string BothExecutorsClaudeResponse =>
        """
        {
            "verdict": "PASSED",
            "recommendation": "approve",
            "scenario_summaries": [
                { "scenario_id": "api-1", "title": "GET /dashboard", "passed": true, "duration_ms": 80,  "error_summary": null },
                { "scenario_id": "ui-1",  "title": "Dashboard loads",  "passed": true, "duration_ms": 500, "error_summary": null }
            ]
        }
        """;

    // ─── T016-1: both executors present → TestResult persisted, PM comment posted ─

    [Fact]
    public async Task WriteAsync_BothExecutorResults_TestResultPersistedAndCommentPosted()
    {
        // Arrange
        var sut = CreateSut();
        var run = DefaultRun;

        _llmClient.Setup(c => c.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BothExecutorsClaudeResponse);
        _secretResolver.Setup(s => s.ResolveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("cred-value");
        _jiraClient.Setup(j => j.PostCommentAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(JiraCommentResult.Success("jira-comment-99"));

        TestResult? saved = null;
        _testResultRepo.Setup(r => r.SaveAsync(It.IsAny<TestResult>(), It.IsAny<CancellationToken>()))
            .Callback<TestResult, CancellationToken>((r, _) => saved = r)
            .Returns(Task.CompletedTask);

        // Act
        await sut.WriteAsync(DefaultStory, BothExecutorsResult, JiraProject, run, postBackEnabled: true);

        // Assert
        Assert.Equal(TestRunStatus.Completed, run.Status);
        Assert.Equal("jira-comment-99", run.PmCommentId);

        Assert.NotNull(saved);
        Assert.Equal("PASSED", saved.Verdict);
        Assert.Equal(1, saved.TotalApiScenarios);
        Assert.Equal(1, saved.PassedApiScenarios);
        Assert.Equal(1, saved.TotalUiE2eScenarios);
        Assert.Equal(1, saved.PassedUiE2eScenarios);
        Assert.Equal(580, saved.TotalDurationMs);
        Assert.Equal("jira-comment-99", saved.PmCommentId);

        _jiraClient.Verify(j => j.PostCommentAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        _testResultRepo.Verify(r => r.SaveAsync(It.IsAny<TestResult>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ─── T016-2: PM tool unavailable → TestResult still persisted, PmCommentId null ─

    [Fact]
    public async Task WriteAsync_PmToolUnavailable_TestResultPersistedWithNullPmCommentId()
    {
        // Arrange
        var sut = CreateSut();
        var run = DefaultRun;

        _llmClient.Setup(c => c.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BothExecutorsClaudeResponse);
        _secretResolver.Setup(s => s.ResolveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("cred-value");

        // PM tool call throws (unavailable).
        _jiraClient.Setup(j => j.PostCommentAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Service unavailable"));

        TestResult? saved = null;
        _testResultRepo.Setup(r => r.SaveAsync(It.IsAny<TestResult>(), It.IsAny<CancellationToken>()))
            .Callback<TestResult, CancellationToken>((r, _) => saved = r)
            .Returns(Task.CompletedTask);

        // Act — must not throw.
        await sut.WriteAsync(DefaultStory, BothExecutorsResult, JiraProject, run, postBackEnabled: true);

        // Assert
        Assert.Equal(TestRunStatus.Completed, run.Status);
        Assert.Null(run.PmCommentId);

        Assert.NotNull(saved);
        Assert.Null(saved.PmCommentId);
        _testResultRepo.Verify(r => r.SaveAsync(It.IsAny<TestResult>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ─── T016-3: ReportWriterException → Status = ReportFailed ───────────────

    [Fact]
    public async Task WriteAsync_CosmosWriteFails_ThrowsReportWriterExceptionAndSetsStatusReportFailed()
    {
        // Arrange
        var sut = CreateSut();
        var run = DefaultRun;

        _llmClient.Setup(c => c.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BothExecutorsClaudeResponse);
        _secretResolver.Setup(s => s.ResolveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("cred-value");
        _jiraClient.Setup(j => j.PostCommentAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(JiraCommentResult.Success("cmt-1"));

        // Cosmos write fails.
        _testResultRepo.Setup(r => r.SaveAsync(It.IsAny<TestResult>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Cosmos partition unavailable"));

        // Act & Assert
        await Assert.ThrowsAsync<ReportWriterException>(() =>
            sut.WriteAsync(DefaultStory, BothExecutorsResult, JiraProject, run, postBackEnabled: true));

        Assert.Equal(TestRunStatus.ReportFailed, run.Status);
    }
}
