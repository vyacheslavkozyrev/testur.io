using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Testurio.Core.Entities;
using Testurio.Core.Enums;
using Testurio.Core.Exceptions;
using Testurio.Core.Interfaces;
using Testurio.Core.Models;
using Testurio.Core.Repositories;

namespace Testurio.UnitTests.Pipeline.ReportWriter;

/// <summary>
/// Unit tests for <see cref="Testurio.Pipeline.ReportWriter.ReportWriter"/> covering all
/// acceptance-criteria branches defined in feature 0030 (US-001 through US-003).
/// Claude, IPmToolClient-equivalents, and ITestResultRepository are mocked — no I/O.
/// </summary>
public class ReportWriterTests
{
    private readonly Mock<ILlmGenerationClient> _llmClient = new();
    private readonly Mock<IJiraApiClient> _jiraClient = new();
    private readonly Mock<IADOClient> _adoClient = new();
    private readonly Mock<ISecretResolver> _secretResolver = new();
    private readonly Mock<ITestResultRepository> _testResultRepo = new();

    private Testurio.Pipeline.ReportWriter.ReportWriter CreateSut() =>
        new(_llmClient.Object, _jiraClient.Object, _adoClient.Object,
            _secretResolver.Object, _testResultRepo.Object,
            NullLogger<Testurio.Pipeline.ReportWriter.ReportWriter>.Instance);

    // ─── Shared fixtures ──────────────────────────────────────────────────────

    private static ParsedStory DefaultStory => new()
    {
        Title = "User can view dashboard",
        Description = "As a user I want to see the dashboard",
        AcceptanceCriteria = ["Dashboard is displayed"]
    };

    private static Project DefaultProject => new()
    {
        UserId = "user1",
        Name = "Test Project",
        ProductUrl = "https://staging.example.com",
        TestingStrategy = "api",
        PmTool = PMToolType.Jira,
        JiraBaseUrl = "https://jira.example.com",
        JiraEmailSecretUri = "https://vault/jira-email",
        JiraApiTokenSecretUri = "https://vault/jira-token",
    };

    private static TestRun DefaultRun => new()
    {
        ProjectId = "proj1",
        UserId = "user1",
        JiraIssueKey = "TEST-123",
        JiraIssueId = "10001",
        ExecutionWarnings = []
    };

    private static ExecutionResult PassedApiExecution => new()
    {
        ApiResults = [new ApiScenarioResult
        {
            ScenarioId = "sc1", Title = "GET /users", Passed = true,
            DurationMs = 100, AssertionResults = []
        }],
        UiE2eResults = []
    };

    private static ExecutionResult FailedApiExecution => new()
    {
        ApiResults = [new ApiScenarioResult
        {
            ScenarioId = "sc1", Title = "GET /users", Passed = false,
            DurationMs = 100, AssertionResults = [
                new AssertionResult { Type = "status_code", Passed = false,
                    Expected = "200", Actual = "401" }
            ]
        }],
        UiE2eResults = []
    };

    private static string BuildValidClaudeResponse(
        string verdict = "PASSED",
        string recommendation = "approve",
        string scenarioId = "sc1",
        string scenarioTitle = "GET /users",
        bool passed = true,
        string? errorSummary = null) =>
        $$"""
        {
            "verdict": "{{verdict}}",
            "recommendation": "{{recommendation}}",
            "scenario_summaries": [
                {
                    "scenario_id": "{{scenarioId}}",
                    "title": "{{scenarioTitle}}",
                    "passed": {{passed.ToString().ToLower()}},
                    "duration_ms": 100,
                    "error_summary": {{(errorSummary is null ? "null" : $"\"{errorSummary}\"")}}
                }
            ]
        }
        """;

    // ─── Happy path ────────────────────────────────────────────────────────────

    [Fact]
    public async Task WriteAsync_HappyPath_PostsCommentAndPersistsResult()
    {
        // Arrange
        var sut = CreateSut();
        var run = DefaultRun;
        var project = DefaultProject;

        _llmClient.Setup(c => c.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildValidClaudeResponse());
        _secretResolver.Setup(s => s.ResolveAsync("https://vault/jira-email", It.IsAny<CancellationToken>()))
            .ReturnsAsync("test@example.com");
        _secretResolver.Setup(s => s.ResolveAsync("https://vault/jira-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync("token123");
        _jiraClient.Setup(j => j.PostCommentAsync(
                project.JiraBaseUrl!, run.JiraIssueKey,
                "test@example.com", "token123",
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(JiraCommentResult.Success("comment-42"));

        TestResult? savedResult = null;
        _testResultRepo.Setup(r => r.SaveAsync(It.IsAny<TestResult>(), It.IsAny<CancellationToken>()))
            .Callback<TestResult, CancellationToken>((r, _) => savedResult = r)
            .Returns(Task.CompletedTask);

        // Act
        await sut.WriteAsync(DefaultStory, PassedApiExecution, project, run, postBackEnabled: true);

        // Assert
        Assert.Equal("comment-42", run.PmCommentId);
        Assert.Equal(TestRunStatus.Completed, run.Status);
        Assert.NotNull(savedResult);
        Assert.Equal("PASSED", savedResult.Verdict);
        Assert.Equal("approve", savedResult.Recommendation);
        Assert.Equal("comment-42", savedResult.PmCommentId);
        Assert.Equal(run.Id, savedResult.RunId);
    }

    // ─── AC-006: retry on parse failure ───────────────────────────────────────

    [Fact]
    public async Task WriteAsync_ClaudeParseFailsOnceThenSucceeds_RetriesAndSaves()
    {
        // Arrange
        var sut = CreateSut();
        var run = DefaultRun;
        var callCount = 0;

        _llmClient.Setup(c => c.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                return callCount == 1 ? "invalid json response" : BuildValidClaudeResponse();
            });
        _secretResolver.Setup(s => s.ResolveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("value");
        _jiraClient.Setup(j => j.PostCommentAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(JiraCommentResult.Success("cmt-1"));
        _testResultRepo.Setup(r => r.SaveAsync(It.IsAny<TestResult>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await sut.WriteAsync(DefaultStory, PassedApiExecution, DefaultProject, run, postBackEnabled: true);

        // Assert — Claude was called exactly twice.
        _llmClient.Verify(c => c.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
        Assert.Equal(TestRunStatus.Completed, run.Status);
    }

    // ─── AC-006: both attempts fail → ReportWriterException ───────────────────

    [Fact]
    public async Task WriteAsync_ClaudeFailsBothAttempts_ThrowsReportWriterException()
    {
        // Arrange
        var sut = CreateSut();

        _llmClient.Setup(c => c.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("not valid json at all");

        // Act & Assert
        await Assert.ThrowsAsync<ReportWriterException>(() =>
            sut.WriteAsync(DefaultStory, PassedApiExecution, DefaultProject, DefaultRun, postBackEnabled: true));
    }

    // ─── AC-016: PM tool post failure → warning logged, Cosmos still written ──

    [Fact]
    public async Task WriteAsync_JiraPostThrows_PmCommentIdIsNullCosmosStillWritten()
    {
        // Arrange
        var sut = CreateSut();
        var run = DefaultRun;

        _llmClient.Setup(c => c.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildValidClaudeResponse());
        _secretResolver.Setup(s => s.ResolveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("value");
        _jiraClient.Setup(j => j.PostCommentAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        TestResult? savedResult = null;
        _testResultRepo.Setup(r => r.SaveAsync(It.IsAny<TestResult>(), It.IsAny<CancellationToken>()))
            .Callback<TestResult, CancellationToken>((r, _) => savedResult = r)
            .Returns(Task.CompletedTask);

        // Act — must not throw (AC-016).
        await sut.WriteAsync(DefaultStory, PassedApiExecution, DefaultProject, run, postBackEnabled: true);

        // Assert
        Assert.Null(run.PmCommentId);
        Assert.Equal(TestRunStatus.Completed, run.Status);
        Assert.NotNull(savedResult);
        Assert.Null(savedResult.PmCommentId);
    }

    // ─── AC-020: Cosmos write failure → ReportWriterException, status ReportFailed ─

    [Fact]
    public async Task WriteAsync_CosmosWriteFails_ThrowsReportWriterExceptionAndSetsStatusReportFailed()
    {
        // Arrange
        var sut = CreateSut();
        var run = DefaultRun;

        _llmClient.Setup(c => c.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildValidClaudeResponse());
        _secretResolver.Setup(s => s.ResolveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("value");
        _jiraClient.Setup(j => j.PostCommentAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(JiraCommentResult.Success("cmt-1"));
        _testResultRepo.Setup(r => r.SaveAsync(It.IsAny<TestResult>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Cosmos unavailable"));

        // Act & Assert
        await Assert.ThrowsAsync<ReportWriterException>(() =>
            sut.WriteAsync(DefaultStory, PassedApiExecution, DefaultProject, run, postBackEnabled: true));

        Assert.Equal(TestRunStatus.ReportFailed, run.Status);
    }

    // ─── AC-003: verdict invariant mismatch ───────────────────────────────────

    [Fact]
    public async Task WriteAsync_ClaudeVerdictMismatches_ThrowsReportWriterException()
    {
        // Arrange — Claude says PASSED but execution has a failure.
        var sut = CreateSut();

        _llmClient.Setup(c => c.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildValidClaudeResponse("PASSED", "approve", "sc1", "GET /users", true));

        // Act & Assert — execution has a failure, so verdict PASSED is wrong.
        await Assert.ThrowsAsync<ReportWriterException>(() =>
            sut.WriteAsync(DefaultStory, FailedApiExecution, DefaultProject, DefaultRun, postBackEnabled: true));
    }

    // ─── AC-007: cancellation token forwarded ─────────────────────────────────

    [Fact]
    public async Task WriteAsync_CancellationTokenForwardedToLlmClient()
    {
        // Arrange
        var sut = CreateSut();
        using var cts = new CancellationTokenSource();
        var capturedToken = CancellationToken.None;

        _llmClient.Setup(c => c.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>((_, _, ct) => capturedToken = ct)
            .ReturnsAsync(BuildValidClaudeResponse());
        _secretResolver.Setup(s => s.ResolveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("value");
        _jiraClient.Setup(j => j.PostCommentAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(JiraCommentResult.Success("cmt-1"));
        _testResultRepo.Setup(r => r.SaveAsync(It.IsAny<TestResult>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await sut.WriteAsync(DefaultStory, PassedApiExecution, DefaultProject, DefaultRun, postBackEnabled: true, cts.Token);

        // Assert — token is forwarded on first call.
        Assert.Equal(cts.Token, capturedToken);
    }

    // ─── AC-007: cancellation token forwarded to retry call ──────────────────

    [Fact]
    public async Task WriteAsync_CancellationTokenForwardedToRetryLlmCall()
    {
        // Arrange
        var sut = CreateSut();
        using var cts = new CancellationTokenSource();
        var capturedTokens = new List<CancellationToken>();
        var callCount = 0;

        _llmClient.Setup(c => c.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>((_, _, ct) => capturedTokens.Add(ct))
            .ReturnsAsync(() =>
            {
                callCount++;
                return callCount == 1 ? "invalid json response" : BuildValidClaudeResponse();
            });
        _secretResolver.Setup(s => s.ResolveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("value");
        _jiraClient.Setup(j => j.PostCommentAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(JiraCommentResult.Success("cmt-1"));
        _testResultRepo.Setup(r => r.SaveAsync(It.IsAny<TestResult>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await sut.WriteAsync(DefaultStory, PassedApiExecution, DefaultProject, DefaultRun, postBackEnabled: true, cts.Token);

        // Assert — token is forwarded on both the first attempt and the retry (AC-007).
        Assert.Equal(2, capturedTokens.Count);
        Assert.All(capturedTokens, ct => Assert.Equal(cts.Token, ct));
    }

    // ─── Feature 0018: BuildScenarioSummaries step mapping ───────────────────

    [Fact]
    public async Task WriteAsync_ApiResult_ScenarioSummaryHasNullSteps()
    {
        // AC-024: API scenario summaries must have Steps == null.
        var sut = CreateSut();
        var execution = PassedApiExecution;

        _llmClient.Setup(c => c.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildValidClaudeResponse());
        _secretResolver.Setup(s => s.ResolveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("value");
        _jiraClient.Setup(j => j.PostCommentAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(JiraCommentResult.Success("cmt-1"));

        TestResult? saved = null;
        _testResultRepo.Setup(r => r.SaveAsync(It.IsAny<TestResult>(), It.IsAny<CancellationToken>()))
            .Callback<TestResult, CancellationToken>((r, _) => saved = r)
            .Returns(Task.CompletedTask);

        await sut.WriteAsync(DefaultStory, execution, DefaultProject, DefaultRun, postBackEnabled: true);

        Assert.NotNull(saved);
        var apiSummary = saved.ScenarioResults.Single(s => s.TestType == "api");
        Assert.Null(apiSummary.Steps);
    }

    [Fact]
    public async Task WriteAsync_UiE2eResult_ScenarioSummaryHasCorrectSteps()
    {
        // AC-010: UI E2E scenario summaries have a non-null Steps list with correct fields.
        var sut = CreateSut();
        var execution = new ExecutionResult
        {
            ApiResults = [],
            UiE2eResults = [new UiE2eScenarioResult
            {
                ScenarioId = "u1",
                Title = "Login flow",
                Passed = true,
                DurationMs = 800,
                StepResults = [
                    new StepExecutionResult { StepIndex = 0, Action = "navigate", Passed = true,  ErrorMessage = null, ScreenshotBlobUri = null },
                    new StepExecutionResult { StepIndex = 1, Action = "click",    Passed = true,  ErrorMessage = null, ScreenshotBlobUri = null },
                    new StepExecutionResult { StepIndex = 2, Action = "assert_url", Passed = true, ErrorMessage = null, ScreenshotBlobUri = null },
                ]
            }]
        };

        const string response = """
            {
                "verdict": "PASSED",
                "recommendation": "approve",
                "scenario_summaries": [
                    { "scenario_id": "u1", "title": "Login flow", "passed": true, "duration_ms": 800, "error_summary": null }
                ]
            }
            """;

        _llmClient.Setup(c => c.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);
        _secretResolver.Setup(s => s.ResolveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("value");
        _jiraClient.Setup(j => j.PostCommentAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(JiraCommentResult.Success("cmt-1"));

        TestResult? saved = null;
        _testResultRepo.Setup(r => r.SaveAsync(It.IsAny<TestResult>(), It.IsAny<CancellationToken>()))
            .Callback<TestResult, CancellationToken>((r, _) => saved = r)
            .Returns(Task.CompletedTask);

        await sut.WriteAsync(DefaultStory, execution, DefaultProject, DefaultRun, postBackEnabled: true);

        Assert.NotNull(saved);
        var uiSummary = saved.ScenarioResults.Single(s => s.TestType == "ui_e2e");
        Assert.NotNull(uiSummary.Steps);
        Assert.Equal(3, uiSummary.Steps.Count);

        // AC-010: StepIndex is 1-based.
        Assert.Equal(1, uiSummary.Steps[0].StepIndex);
        Assert.Equal("navigate", uiSummary.Steps[0].Action);
        Assert.True(uiSummary.Steps[0].Passed);
        Assert.Null(uiSummary.Steps[0].ErrorMessage);
        Assert.Null(uiSummary.Steps[0].ScreenshotBlobUri);

        Assert.Equal(2, uiSummary.Steps[1].StepIndex);
        Assert.Equal("click", uiSummary.Steps[1].Action);

        Assert.Equal(3, uiSummary.Steps[2].StepIndex);
        Assert.Equal("assert_url", uiSummary.Steps[2].Action);
    }

    [Fact]
    public async Task WriteAsync_MixedApiAndUiE2e_StepsNullabilityIsCorrect()
    {
        // AC-010: mixed run — API summary has null Steps, UI E2E summary has non-null Steps.
        var sut = CreateSut();
        var execution = new ExecutionResult
        {
            ApiResults = [new ApiScenarioResult
            {
                ScenarioId = "a1", Title = "GET /users", Passed = true,
                DurationMs = 100, AssertionResults = []
            }],
            UiE2eResults = [new UiE2eScenarioResult
            {
                ScenarioId = "u1",
                Title = "Login flow",
                Passed = false,
                DurationMs = 500,
                StepResults = [
                    new StepExecutionResult
                    {
                        StepIndex = 0, Action = "assert_text", Passed = false,
                        ErrorMessage = "Expected 'Hello' but got 'Error'",
                        ScreenshotBlobUri = "https://blob.example.com/step1.png"
                    }
                ]
            }]
        };

        const string response = """
            {
                "verdict": "FAILED",
                "recommendation": "request_fixes",
                "scenario_summaries": [
                    { "scenario_id": "a1", "title": "GET /users",  "passed": true,  "duration_ms": 100, "error_summary": null },
                    { "scenario_id": "u1", "title": "Login flow",  "passed": false, "duration_ms": 500, "error_summary": "Step 1 failed" }
                ]
            }
            """;

        _llmClient.Setup(c => c.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);
        _secretResolver.Setup(s => s.ResolveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("value");
        _jiraClient.Setup(j => j.PostCommentAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(JiraCommentResult.Success("cmt-1"));

        TestResult? saved = null;
        _testResultRepo.Setup(r => r.SaveAsync(It.IsAny<TestResult>(), It.IsAny<CancellationToken>()))
            .Callback<TestResult, CancellationToken>((r, _) => saved = r)
            .Returns(Task.CompletedTask);

        await sut.WriteAsync(DefaultStory, execution, DefaultProject, DefaultRun, postBackEnabled: true);

        Assert.NotNull(saved);
        var apiSummary = saved.ScenarioResults.Single(s => s.TestType == "api");
        var uiSummary = saved.ScenarioResults.Single(s => s.TestType == "ui_e2e");

        // API → null steps.
        Assert.Null(apiSummary.Steps);

        // UI E2E → non-null steps with correct detail.
        Assert.NotNull(uiSummary.Steps);
        Assert.Single(uiSummary.Steps);
        var step = uiSummary.Steps[0];
        Assert.Equal(1, step.StepIndex);
        Assert.Equal("assert_text", step.Action);
        Assert.False(step.Passed);
        Assert.Equal("Expected 'Hello' but got 'Error'", step.ErrorMessage);
        Assert.Equal("https://blob.example.com/step1.png", step.ScreenshotBlobUri);
    }

    // ─── AC-018: TestResult counts ────────────────────────────────────────────

    [Fact]
    public async Task WriteAsync_HappyPath_TestResultCountsAreCorrect()
    {
        // Arrange
        var sut = CreateSut();
        var execution = new ExecutionResult
        {
            ApiResults = [
                new ApiScenarioResult { ScenarioId = "a1", Title = "A1", Passed = true,  DurationMs = 50, AssertionResults = [] },
                new ApiScenarioResult { ScenarioId = "a2", Title = "A2", Passed = false, DurationMs = 60, AssertionResults = [] },
            ],
            UiE2eResults = [
                new UiE2eScenarioResult { ScenarioId = "u1", Title = "U1", Passed = true, DurationMs = 200, StepResults = [] },
            ]
        };

        const string response = """
            {
                "verdict": "FAILED",
                "recommendation": "request_fixes",
                "scenario_summaries": [
                    { "scenario_id": "a1", "title": "A1", "passed": true,  "duration_ms": 50, "error_summary": null },
                    { "scenario_id": "a2", "title": "A2", "passed": false, "duration_ms": 60, "error_summary": "Expected: 200 / Actual: 500" },
                    { "scenario_id": "u1", "title": "U1", "passed": true,  "duration_ms": 200, "error_summary": null }
                ]
            }
            """;

        _llmClient.Setup(c => c.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);
        _secretResolver.Setup(s => s.ResolveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("value");
        _jiraClient.Setup(j => j.PostCommentAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(JiraCommentResult.Success("cmt-1"));

        TestResult? saved = null;
        _testResultRepo.Setup(r => r.SaveAsync(It.IsAny<TestResult>(), It.IsAny<CancellationToken>()))
            .Callback<TestResult, CancellationToken>((r, _) => saved = r)
            .Returns(Task.CompletedTask);

        // Act
        await sut.WriteAsync(DefaultStory, execution, DefaultProject, DefaultRun, postBackEnabled: true);

        // Assert
        Assert.NotNull(saved);
        Assert.Equal(2, saved.TotalApiScenarios);
        Assert.Equal(1, saved.PassedApiScenarios);
        Assert.Equal(1, saved.TotalUiE2eScenarios);
        Assert.Equal(1, saved.PassedUiE2eScenarios);
        Assert.Equal(310, saved.TotalDurationMs); // 50 + 60 + 200
    }
}
