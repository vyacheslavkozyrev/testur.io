using System.Text.Json;
using Microsoft.Extensions.Logging;
using Testurio.Core.Entities;
using Testurio.Core.Enums;
using Testurio.Core.Exceptions;
using Testurio.Core.Interfaces;
using Testurio.Core.Models;
using Testurio.Core.Repositories;

namespace Testurio.Pipeline.ReportWriter;

/// <summary>
/// Stage 6 of the pipeline (feature 0030).
/// Generates a structured verdict report via Claude, formats and posts a PM tool comment,
/// and persists a <see cref="TestResult"/> document to Cosmos DB.
/// </summary>
public sealed partial class ReportWriter : IReportWriter
{
    private const string SystemPrompt =
        """
        You are a QA analyst assistant. You will be given a test execution result and must produce a structured JSON report.
        Return ONLY valid JSON — no markdown fences, no commentary.
        The JSON must have exactly three top-level fields: "verdict", "recommendation", and "scenario_summaries".

        Rules:
        - "verdict": "PASSED" if every scenario passed, "FAILED" otherwise.
        - "recommendation": one of exactly "approve", "request_fixes", or "flag_for_manual_review".
          - "approve" when verdict is "PASSED" and no execution warnings.
          - "request_fixes" when verdict is "FAILED" and all failures have clear assertion diffs or step errors (no infrastructure exceptions).
          - "flag_for_manual_review" when verdict is "FAILED" and at least one failure is an infrastructure-level exception, or when execution warnings are present.
        - "scenario_summaries": array of objects, one per executed scenario, in execution order.
          Each object: { "scenario_id": string, "title": string, "passed": bool, "duration_ms": number, "error_summary": string|null }.
          For failed API scenarios: error_summary lists assertion diffs as "Expected: <v> / Actual: <v>".
          For failed UI E2E scenarios: error_summary lists the first step error message and step index.
          error_summary is null when passed is true.
        """;

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly ILlmGenerationClient _llmClient;
    private readonly IJiraApiClient _jiraApiClient;
    private readonly IADOClient _adoClient;
    private readonly ISecretResolver _secretResolver;
    private readonly ITestResultRepository _testResultRepository;
    private readonly ILogger<ReportWriter> _logger;

    public ReportWriter(
        ILlmGenerationClient llmClient,
        IJiraApiClient jiraApiClient,
        IADOClient adoClient,
        ISecretResolver secretResolver,
        ITestResultRepository testResultRepository,
        ILogger<ReportWriter> logger)
    {
        _llmClient = llmClient;
        _jiraApiClient = jiraApiClient;
        _adoClient = adoClient;
        _secretResolver = secretResolver;
        _testResultRepository = testResultRepository;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task WriteAsync(
        ParsedStory story,
        ExecutionResult execution,
        Project projectConfig,
        TestRun run,
        CancellationToken ct = default)
    {
        // AC-001: call Claude with the serialised execution result, story title, and warnings.
        var reportContent = await GenerateReportContentAsync(story, execution, run, ct);

        // AC-003: validate verdict invariant against the raw execution result.
        ValidateVerdict(reportContent, execution);

        // Build scenario summaries with screenshot URIs from the raw UI E2E results.
        var scenarioSummaries = BuildScenarioSummaries(reportContent, execution);
        var enrichedReport = reportContent with { ScenarioSummaries = scenarioSummaries };

        // Format the markdown comment.
        var commentMarkdown = PmCommentFormatter.Format(enrichedReport, story.Title);

        // AC-013 / AC-014 / AC-015 / AC-016: post comment to PM tool (non-throwing on failure).
        string? commentId = null;
        try
        {
            commentId = await PostCommentAsync(projectConfig, run, commentMarkdown, ct);
        }
        catch (Exception ex)
        {
            // AC-016: log warning and continue — PM post failure does not throw ReportWriterException.
            LogCommentPostFailed(_logger, run.Id, run.JiraIssueKey, ex);
        }

        // AC-015: set PmCommentId in-memory (null when post failed).
        run.PmCommentId = commentId;

        // AC-017 / AC-018 / AC-019 / AC-020 / AC-021: persist TestResult to Cosmos.
        var testResult = BuildTestResult(story, execution, run, enrichedReport, commentMarkdown, commentId);

        try
        {
            await _testResultRepository.SaveAsync(testResult, ct);
        }
        catch (Exception ex)
        {
            // AC-020: Cosmos write failure → status ReportFailed + throw ReportWriterException.
            run.Status = TestRunStatus.ReportFailed;
            throw new ReportWriterException(
                $"Failed to persist TestResult for run {run.Id}: {ex.Message}", ex);
        }

        // AC-020: set status to Completed after successful Cosmos write.
        run.Status = TestRunStatus.Completed;

        LogReportWritten(_logger, run.Id, enrichedReport.Verdict, enrichedReport.Recommendation);
    }

    // ─── Private helpers ──────────────────────────────────────────────────────

    /// <summary>
    /// Calls Claude with the execution result and story context to produce a <see cref="ReportContent"/>.
    /// Retries exactly once if the first parse fails (AC-006 / AC-007).
    /// </summary>
    private async Task<ReportContent> GenerateReportContentAsync(
        ParsedStory story,
        ExecutionResult execution,
        TestRun run,
        CancellationToken ct)
    {
        var userMessage = BuildPrompt(story, execution, run);

        // First attempt.
        string rawResponse;
        try
        {
            rawResponse = await _llmClient.CompleteAsync(SystemPrompt, userMessage, ct);
        }
        catch (Exception ex)
        {
            throw new ReportWriterException(
                $"Claude API call failed on first attempt for run {run.Id}: {ex.Message}", ex);
        }

        if (TryParseReportContent(rawResponse, out var report))
            return report!;

        LogParseFailedRetrying(_logger, run.Id);

        // Retry once (AC-006 / AC-007).
        string retryResponse;
        try
        {
            retryResponse = await _llmClient.CompleteAsync(SystemPrompt, userMessage, ct);
        }
        catch (Exception ex)
        {
            throw new ReportWriterException(
                $"Claude API call failed on retry attempt for run {run.Id}: {ex.Message}", ex);
        }

        if (TryParseReportContent(retryResponse, out var retryReport))
            return retryReport!;

        throw new ReportWriterException(
            $"Claude failed to produce a valid JSON report after 2 attempts for run {run.Id}");
    }

    private static string BuildPrompt(ParsedStory story, ExecutionResult execution, TestRun run)
    {
        var executionJson = JsonSerializer.Serialize(execution, JsonOptions);
        var warningsJson = JsonSerializer.Serialize(run.ExecutionWarnings, JsonOptions);

        return $"""
            Story title: {story.Title}

            Execution warnings: {warningsJson}

            Execution result:
            {executionJson}
            """;
    }

    private static bool TryParseReportContent(string rawResponse, out ReportContent? report)
    {
        report = null;

        // AC-006: extract JSON by locating the first '{' and last '}'.
        var firstBrace = rawResponse.IndexOf('{');
        var lastBrace = rawResponse.LastIndexOf('}');

        if (firstBrace < 0 || lastBrace < firstBrace)
            return false;

        var jsonText = rawResponse[firstBrace..(lastBrace + 1)];

        try
        {
            using var doc = JsonDocument.Parse(jsonText);
            var root = doc.RootElement;

            if (!root.TryGetProperty("verdict", out var verdictEl) ||
                !root.TryGetProperty("recommendation", out var recommendationEl) ||
                !root.TryGetProperty("scenario_summaries", out var summariesEl))
                return false;

            var verdict = verdictEl.GetString();
            var recommendation = recommendationEl.GetString();

            if (string.IsNullOrEmpty(verdict) || string.IsNullOrEmpty(recommendation))
                return false;

            var summaries = new List<ScenarioSummary>();
            foreach (var item in summariesEl.EnumerateArray())
            {
                var id = item.TryGetProperty("scenario_id", out var sid) ? sid.GetString() : null;
                var title = item.TryGetProperty("title", out var t) ? t.GetString() : null;
                var passed = item.TryGetProperty("passed", out var p) && p.GetBoolean();
                var durationMs = item.TryGetProperty("duration_ms", out var d) ? d.GetInt64() : 0L;
                var errorSummary = item.TryGetProperty("error_summary", out var e)
                    ? e.ValueKind == JsonValueKind.Null ? null : e.GetString()
                    : null;

                if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(title))
                    return false;

                // TestType will be enriched from raw execution result in BuildScenarioSummaries.
                summaries.Add(new ScenarioSummary(id, title, passed, durationMs, errorSummary, string.Empty, []));
            }

            report = new ReportContent(verdict, recommendation, summaries.AsReadOnly());
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Validates that Claude's verdict agrees with the raw execution result (AC-003).
    /// Throws <see cref="ReportWriterException"/> when they disagree.
    /// </summary>
    private static void ValidateVerdict(ReportContent report, ExecutionResult execution)
    {
        var allPassed = execution.ApiResults.All(r => r.Passed)
                        && execution.UiE2eResults.All(r => r.Passed);

        var expectedVerdict = allPassed ? "PASSED" : "FAILED";

        if (!string.Equals(report.Verdict, expectedVerdict, StringComparison.OrdinalIgnoreCase))
        {
            throw new ReportWriterException(
                $"Verdict invariant violation: Claude returned '{report.Verdict}' but execution " +
                $"results indicate '{expectedVerdict}'");
        }
    }

    /// <summary>
    /// Enriches the Claude-produced summaries with test type and screenshot URIs from the
    /// raw execution result (AC-008 / AC-011).
    /// </summary>
    private static IReadOnlyList<ScenarioSummary> BuildScenarioSummaries(
        ReportContent report,
        ExecutionResult execution)
    {
        // Build look-up maps by scenario ID.
        var apiIndex = execution.ApiResults.ToDictionary(r => r.ScenarioId, StringComparer.OrdinalIgnoreCase);
        var uiIndex = execution.UiE2eResults.ToDictionary(r => r.ScenarioId, StringComparer.OrdinalIgnoreCase);

        var enriched = new List<ScenarioSummary>(report.ScenarioSummaries.Count);

        foreach (var summary in report.ScenarioSummaries)
        {
            string testType;
            IReadOnlyList<string> screenshotUris;

            if (apiIndex.ContainsKey(summary.ScenarioId))
            {
                testType = "api";
                screenshotUris = [];
            }
            else if (uiIndex.TryGetValue(summary.ScenarioId, out var uiResult))
            {
                testType = "ui_e2e";
                // AC-011: collect screenshot URIs from failed assertion steps.
                screenshotUris = uiResult.StepResults
                    .Where(s => !s.Passed && s.ScreenshotBlobUri is not null)
                    .Select(s => s.ScreenshotBlobUri!)
                    .ToList()
                    .AsReadOnly();
            }
            else
            {
                // Scenario not found in either result list — keep as-is without screenshot data.
                testType = "unknown";
                screenshotUris = [];
            }

            enriched.Add(summary with { TestType = testType, ScreenshotUris = screenshotUris });
        }

        return enriched.AsReadOnly();
    }

    /// <summary>
    /// Posts the comment to Jira or ADO and returns the comment ID assigned by the PM tool.
    /// Returns <c>null</c> when credentials are missing or the post fails.
    /// Does not throw (AC-016 — non-fatal).
    /// </summary>
    private async Task<string?> PostCommentAsync(
        Project project,
        TestRun run,
        string commentMarkdown,
        CancellationToken ct)
    {
        var pmToolType = project.PmTool ?? PMToolType.Jira;

        if (pmToolType == PMToolType.Jira)
            return await PostJiraCommentAsync(project, run, commentMarkdown, ct);

        return await PostAdoCommentAsync(project, run, commentMarkdown, ct);
    }

    private async Task<string?> PostJiraCommentAsync(
        Project project,
        TestRun run,
        string commentBody,
        CancellationToken ct)
    {
        if (string.IsNullOrEmpty(project.JiraEmailSecretUri) || string.IsNullOrEmpty(project.JiraApiTokenSecretUri))
        {
            LogJiraCredentialsMissing(_logger, run.JiraIssueKey);
            return null;
        }

        var email = await _secretResolver.ResolveAsync(project.JiraEmailSecretUri, ct);
        var token = await _secretResolver.ResolveAsync(project.JiraApiTokenSecretUri, ct);

        var result = await _jiraApiClient.PostCommentAsync(
            project.JiraBaseUrl!,
            run.JiraIssueKey,
            email,
            token,
            commentBody,
            ct);

        if (!result.IsSuccess)
        {
            LogCommentPostWarning(_logger, run.Id, run.JiraIssueKey, "Jira",
                result.StatusCode, result.ErrorDetail ?? string.Empty);
            return null;
        }

        LogCommentPosted(_logger, run.Id, run.JiraIssueKey, "Jira");
        return result.CommentId;
    }

    private async Task<string?> PostAdoCommentAsync(
        Project project,
        TestRun run,
        string commentBody,
        CancellationToken ct)
    {
        if (!int.TryParse(run.JiraIssueId, out var workItemId))
        {
            LogAdoWorkItemIdMissing(_logger, run.JiraIssueKey);
            return null;
        }

        if (string.IsNullOrEmpty(project.AdoTokenSecretUri))
        {
            LogAdoCredentialsMissing(_logger, run.JiraIssueKey);
            return null;
        }

        var token = await _secretResolver.ResolveAsync(project.AdoTokenSecretUri, ct);

        var commentId = await _adoClient.PostCommentAsync(
            project.AdoOrgUrl!,
            project.AdoProjectName!,
            workItemId,
            token,
            commentBody,
            ct);

        if (commentId is null)
        {
            LogCommentPostWarning(_logger, run.Id, run.JiraIssueKey, "ADO", 0, "Post returned null");
            return null;
        }

        LogCommentPosted(_logger, run.Id, run.JiraIssueKey, "ADO");
        return commentId;
    }

    private static TestResult BuildTestResult(
        ParsedStory story,
        ExecutionResult execution,
        TestRun run,
        ReportContent report,
        string commentMarkdown,
        string? commentId)
    {
        var passedApi = execution.ApiResults.Count(r => r.Passed);
        var passedUiE2e = execution.UiE2eResults.Count(r => r.Passed);
        var totalDurationMs = execution.ApiResults.Sum(r => r.DurationMs)
                              + execution.UiE2eResults.Sum(r => r.DurationMs);

        return new TestResult
        {
            Id = Guid.NewGuid().ToString(),
            RunId = run.Id,
            ProjectId = run.ProjectId,
            UserId = run.UserId,
            StoryTitle = story.Title,
            Verdict = report.Verdict,
            Recommendation = report.Recommendation,
            TotalApiScenarios = execution.ApiResults.Count,
            PassedApiScenarios = passedApi,
            TotalUiE2eScenarios = execution.UiE2eResults.Count,
            PassedUiE2eScenarios = passedUiE2e,
            TotalDurationMs = totalDurationMs,
            PmCommentId = commentId,
            ScenarioResults = report.ScenarioSummaries,
            RawCommentMarkdown = commentMarkdown,
            CreatedAt = DateTimeOffset.UtcNow,
        };
    }

    // ─── Structured log messages ──────────────────────────────────────────────

    [LoggerMessage(Level = LogLevel.Information,
        Message = "ReportWriter: report written for run {RunId} — verdict: {Verdict}, recommendation: {Recommendation}")]
    private static partial void LogReportWritten(ILogger logger, string runId, string verdict, string recommendation);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "ReportWriter: Claude parse failed on first attempt for run {RunId} — retrying")]
    private static partial void LogParseFailedRetrying(ILogger logger, string runId);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "ReportWriter: PM tool comment post failed for run {RunId} / issue {IssueKey}")]
    private static partial void LogCommentPostFailed(ILogger logger, string runId, string issueKey, Exception ex);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "ReportWriter: {PmTool} comment post failed for run {RunId} / issue {IssueKey} — HTTP {StatusCode}: {ErrorDetail}")]
    private static partial void LogCommentPostWarning(
        ILogger logger, string runId, string issueKey, string pmTool, int statusCode, string errorDetail);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "ReportWriter: comment posted to {IssueKey} via {PmTool} for run {RunId}")]
    private static partial void LogCommentPosted(ILogger logger, string runId, string issueKey, string pmTool);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "ReportWriter: Jira credentials not configured for {IssueKey} — comment skipped")]
    private static partial void LogJiraCredentialsMissing(ILogger logger, string issueKey);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "ReportWriter: ADO work item ID could not be parsed for {IssueKey} — comment skipped")]
    private static partial void LogAdoWorkItemIdMissing(ILogger logger, string issueKey);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "ReportWriter: ADO token not configured for {IssueKey} — comment skipped")]
    private static partial void LogAdoCredentialsMissing(ILogger logger, string issueKey);
}
