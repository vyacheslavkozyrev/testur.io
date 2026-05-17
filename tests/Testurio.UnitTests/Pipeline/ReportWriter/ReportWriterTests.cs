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
        await sut.WriteAsync(DefaultStory, PassedApiExecution, project, run);

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
        await sut.WriteAsync(DefaultStory, PassedApiExecution, DefaultProject, run);

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
            sut.WriteAsync(DefaultStory, PassedApiExecution, DefaultProject, DefaultRun));
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
        await sut.WriteAsync(DefaultStory, PassedApiExecution, DefaultProject, run);

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
            sut.WriteAsync(DefaultStory, PassedApiExecution, DefaultProject, run));

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
            sut.WriteAsync(DefaultStory, FailedApiExecution, DefaultProject, DefaultRun));
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
        await sut.WriteAsync(DefaultStory, PassedApiExecution, DefaultProject, DefaultRun, cts.Token);

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
        await sut.WriteAsync(DefaultStory, PassedApiExecution, DefaultProject, DefaultRun, cts.Token);

        // Assert — token is forwarded on both the first attempt and the retry (AC-007).
        Assert.Equal(2, capturedTokens.Count);
        Assert.All(capturedTokens, ct => Assert.Equal(cts.Token, ct));
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
        await sut.WriteAsync(DefaultStory, execution, DefaultProject, DefaultRun);

        // Assert
        Assert.NotNull(saved);
        Assert.Equal(2, saved.TotalApiScenarios);
        Assert.Equal(1, saved.PassedApiScenarios);
        Assert.Equal(1, saved.TotalUiE2eScenarios);
        Assert.Equal(1, saved.PassedUiE2eScenarios);
        Assert.Equal(310, saved.TotalDurationMs); // 50 + 60 + 200
    }
}
