using System.Text;
using Testurio.Core.Entities;
using Testurio.Core.Enums;

namespace Testurio.Plugins.ReportWriterPlugin;


/// <summary>
/// Assembles a Jira markdown comment string from test run data.
/// Pure, stateless service — no I/O, no side effects.
/// </summary>
public class ReportBuilderService
{
    /// <summary>
    /// Builds the full Jira comment body for a completed test run.
    /// </summary>
    /// <param name="run">The completed test run.</param>
    /// <param name="scenarios">All scenarios that were generated for this run.</param>
    /// <param name="stepResults">All step results recorded for this run.</param>
    /// <param name="logSection">
    /// Optional pre-formatted log section appended after the full breakdown.
    /// Feature 0005 uses this parameter to attach execution log content without
    /// modifying core builder logic.
    /// </param>
    public string Build(
        TestRun run,
        IReadOnlyList<TestScenario> scenarios,
        IReadOnlyList<StepResult> stepResults,
        string? logSection = null)
    {
        var sb = new StringBuilder();

        AppendSummaryHeader(sb, run, scenarios, stepResults);

        var failedSteps = stepResults
            .Where(s => s.Status is StepStatus.Failed or StepStatus.Error or StepStatus.Timeout)
            .ToList();

        if (run.Status == TestRunStatus.Failed && failedSteps.Count > 0)
        {
            AppendFailuresSection(sb, scenarios, failedSteps);
        }

        AppendFullBreakdown(sb, scenarios, stepResults);

        if (!string.IsNullOrWhiteSpace(logSection))
        {
            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine();
            sb.Append(logSection);
        }

        return sb.ToString().TrimEnd();
    }

    private static void AppendSummaryHeader(
        StringBuilder sb,
        TestRun run,
        IReadOnlyList<TestScenario> scenarios,
        IReadOnlyList<StepResult> stepResults)
    {
        var overallStatus = run.Status == TestRunStatus.Completed ? "Passed" : "Failed";
        var passCount = stepResults.Count(s => s.Status == StepStatus.Passed);
        var failCount = stepResults.Count(s => s.Status != StepStatus.Passed);
        var timestamp = run.CompletedAt?.ToString("yyyy-MM-dd HH:mm:ss UTC") ?? run.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss UTC");

        sb.AppendLine($"*Testurio Test Report — {overallStatus}*");
        sb.AppendLine();
        sb.AppendLine($"*Run completed:* {timestamp}");
        sb.AppendLine($"*Scenarios:* {scenarios.Count}");
        sb.AppendLine($"*Steps:* {stepResults.Count} total — {passCount} passed, {failCount} failed");
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();
    }

    private static void AppendFailuresSection(
        StringBuilder sb,
        IReadOnlyList<TestScenario> scenarios,
        IReadOnlyList<StepResult> failedSteps)
    {
        sb.AppendLine("*Failures*");
        sb.AppendLine();

        foreach (var step in failedSteps)
        {
            var scenario = scenarios.FirstOrDefault(s => s.Id == step.ScenarioId);
            var scenarioTitle = scenario?.Title ?? "(unknown scenario)";

            sb.AppendLine($"* *Scenario:* {scenarioTitle} | *Step:* {step.StepTitle}");

            var reason = FormatFailureReason(step);
            sb.AppendLine($"  * Reason: {reason}");

            if (step.ActualStatusCode.HasValue || step.ExpectedStatusCode > 0)
            {
                sb.AppendLine($"  * Expected status: {step.ExpectedStatusCode} | Actual status: {step.ActualStatusCode?.ToString() ?? "N/A"}");
            }
        }

        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();
    }

    private static void AppendFullBreakdown(
        StringBuilder sb,
        IReadOnlyList<TestScenario> scenarios,
        IReadOnlyList<StepResult> stepResults)
    {
        sb.AppendLine("*Full Scenario Breakdown*");
        sb.AppendLine();

        foreach (var scenario in scenarios)
        {
            var scenarioSteps = stepResults
                .Where(s => s.ScenarioId == scenario.Id)
                .ToList();

            var scenarioOutcome = scenarioSteps.All(s => s.Status == StepStatus.Passed) ? "Passed" : "Failed";
            sb.AppendLine($"*{scenario.Title}* — {scenarioOutcome}");
            sb.AppendLine();

            foreach (var step in scenarioSteps)
            {
                AppendStepDetail(sb, step);
            }

            sb.AppendLine();
        }
    }

    private static void AppendStepDetail(StringBuilder sb, StepResult step)
    {
        var statusLabel = step.Status.ToString();
        sb.AppendLine($"  * *{step.StepTitle}* — {statusLabel}");
        sb.AppendLine($"    * Expected status code: {step.ExpectedStatusCode}");
        sb.AppendLine($"    * Actual status code: {step.ActualStatusCode?.ToString() ?? "N/A"}");

        if (!string.IsNullOrWhiteSpace(step.ExpectedResponseSchema))
        {
            sb.AppendLine($"    * Expected schema: {{code}}{step.ExpectedResponseSchema}{{code}}");
        }

        if (!string.IsNullOrWhiteSpace(step.ActualResponseBody))
        {
            sb.AppendLine($"    * Actual response: {{code}}{step.ActualResponseBody}{{code}}");
        }

        sb.AppendLine($"    * Duration: {step.DurationMs} ms");

        if (step.Status == StepStatus.Timeout)
        {
            sb.AppendLine($"    * Timed out after {step.DurationMs} ms");
        }

        if (step.Status == StepStatus.Error && !string.IsNullOrWhiteSpace(step.ErrorDescription))
        {
            sb.AppendLine($"    * Error: {step.ErrorDescription}");
        }
    }

    private static string FormatFailureReason(StepResult step)
    {
        return step.Status switch
        {
            StepStatus.Timeout => $"Timeout after {step.DurationMs} ms",
            StepStatus.Error when !string.IsNullOrWhiteSpace(step.ErrorDescription) => step.ErrorDescription,
            StepStatus.Error => "Error — invalid request definition",
            StepStatus.Failed => $"Status mismatch — expected {step.ExpectedStatusCode}, got {step.ActualStatusCode?.ToString() ?? "N/A"}",
            _ => step.Status.ToString()
        };
    }

    // — Template-based rendering (feature 0009) —

    /// <summary>
    /// Renders a report using <paramref name="template"/> by substituting all supported placeholder tokens
    /// with data derived from the run, scenarios, step results, log entries, and project settings.
    /// Delegates token substitution to <see cref="TemplateRenderer"/>.
    /// </summary>
    /// <param name="template">The Markdown template content (custom or built-in default).</param>
    /// <param name="run">The completed test run.</param>
    /// <param name="scenarios">All scenarios generated for this run.</param>
    /// <param name="stepResults">All step results for this run.</param>
    /// <param name="logEntries">Execution log entries; empty when logs are not available.</param>
    /// <param name="reportIncludeLogs">Whether to expand <c>{{logs}}</c> (AC-032).</param>
    /// <param name="reportIncludeScreenshots">Whether to expand <c>{{screenshots}}</c> (AC-032).</param>
    /// <param name="storyTitle">Work item title from the PM tool.</param>
    /// <param name="storyUrl">Direct URL to the work item.</param>
    /// <param name="aiScenarioSource">Raw AI-generated scenario text before execution.</param>
    public string BuildFromTemplate(
        string template,
        TestRun run,
        IReadOnlyList<TestScenario> scenarios,
        IReadOnlyList<StepResult> stepResults,
        IReadOnlyList<ExecutionLogEntry> logEntries,
        bool reportIncludeLogs,
        bool reportIncludeScreenshots,
        string? storyTitle = null,
        string? storyUrl = null,
        string? aiScenarioSource = null)
    {
        var logSection = reportIncludeLogs && logEntries.Count > 0
            ? BuildLogSection(logEntries, scenarios)
            : string.Empty;

        var context = new TemplateRenderContext(
            Run: run,
            Scenarios: scenarios,
            StepResults: stepResults,
            LogSection: logSection,
            IncludeScreenshots: reportIncludeScreenshots,
            StoryTitle: storyTitle,
            StoryUrl: storyUrl,
            AiScenarioSource: aiScenarioSource);

        return TemplateRenderer.Render(template, context);
    }

    // — Execution log section (AC-012 – AC-015) —

    /// <summary>
    /// Builds a pre-formatted log section from a list of <see cref="ExecutionLogEntry"/> records.
    /// The returned string is intended to be passed as <paramref name="logSection"/> in <see cref="Build"/>.
    /// </summary>
    /// <param name="logEntries">All log entries for the run, ordered by scenario and step index.</param>
    /// <param name="scenarios">Scenarios for the run — used to group log entries by scenario title.</param>
    public string BuildLogSection(
        IReadOnlyList<ExecutionLogEntry> logEntries,
        IReadOnlyList<TestScenario> scenarios)
    {
        if (logEntries.Count == 0)
            return string.Empty;

        var sb = new StringBuilder();
        sb.AppendLine("*Execution Logs*");
        sb.AppendLine();

        // Group entries by scenario for readable output.
        var grouped = logEntries
            .GroupBy(e => e.ScenarioId)
            .ToList();

        foreach (var group in grouped)
        {
            var scenario = scenarios.FirstOrDefault(s => s.Id == group.Key);
            var scenarioTitle = scenario?.Title ?? group.Key;

            sb.AppendLine($"*{scenarioTitle}*");
            sb.AppendLine();

            foreach (var entry in group.OrderBy(e => e.StepIndex))
            {
                AppendLogEntry(sb, entry);
            }
        }

        return sb.ToString();
    }

    private static void AppendLogEntry(StringBuilder sb, ExecutionLogEntry entry)
    {
        sb.AppendLine($"  *Step {entry.StepIndex}: {entry.StepTitle}*");
        sb.AppendLine();

        // — Request —
        sb.AppendLine("    *Request*");
        sb.AppendLine($"    {{code}}");
        sb.AppendLine($"{entry.HttpMethod} {entry.RequestUrl}");
        foreach (var (name, value) in entry.RequestHeaders)
            sb.AppendLine($"{name}: {value}");
        if (!string.IsNullOrWhiteSpace(entry.RequestBody))
        {
            sb.AppendLine();
            sb.AppendLine(entry.RequestBody);
        }
        sb.AppendLine($"    {{code}}");
        sb.AppendLine();

        // — Response —
        sb.AppendLine("    *Response*");
        sb.AppendLine($"    {{code}}");

        var status = entry.ResponseStatusCode.HasValue
            ? entry.ResponseStatusCode.Value.ToString()
            : "(no response)";
        sb.AppendLine($"Status: {status}");

        foreach (var (name, value) in entry.ResponseHeaders)
            sb.AppendLine($"{name}: {value}");

        sb.AppendLine();

        if (!string.IsNullOrWhiteSpace(entry.ResponseBodyBlobUrl))
        {
            // AC-014: blob-stored body — include reference URL instead of inlining.
            sb.AppendLine($"[Response body stored in blob: {entry.ResponseBodyBlobUrl}]");
        }
        else if (entry.ResponseTruncated)
        {
            // AC-008: blob upload failed — show truncation notice.
            sb.AppendLine("[Response body truncated — blob upload failed]");
            if (!string.IsNullOrWhiteSpace(entry.ResponseBodyInline))
                sb.AppendLine(entry.ResponseBodyInline);
        }
        else if (!string.IsNullOrWhiteSpace(entry.ResponseBodyInline))
        {
            sb.AppendLine(entry.ResponseBodyInline);
        }
        else
        {
            sb.AppendLine("(empty)");
        }

        sb.AppendLine($"    {{code}}");

        if (!string.IsNullOrWhiteSpace(entry.ErrorDetail))
            sb.AppendLine($"    *Error:* {entry.ErrorDetail}");

        sb.AppendLine();
    }
}
