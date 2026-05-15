using System.Text;
using Testurio.Core.Entities;
using Testurio.Core.Enums;

namespace Testurio.Plugins.ReportWriterPlugin;

/// <summary>
/// Context object passed to <see cref="TemplateRenderer.Render"/> containing all run data
/// needed for placeholder token substitution.
/// </summary>
public record TemplateRenderContext(
    TestRun Run,
    IReadOnlyList<TestScenario> Scenarios,
    IReadOnlyList<StepResult> StepResults,
    string LogSection,
    bool IncludeScreenshots,
    string? StoryTitle = null,
    string? StoryUrl = null,
    string? AiScenarioSource = null);

/// <summary>
/// Performs placeholder token substitution on a Markdown template.
/// Recognised tokens are replaced with run data; unrecognised <c>{{...}}</c> patterns are left as-is (AC-020).
/// Missing data values are replaced with an empty string (AC-021).
/// Token matching is case-sensitive (AC-020).
/// </summary>
public static class TemplateRenderer
{
    /// <summary>
    /// Substitutes all recognised placeholder tokens in <paramref name="template"/> with values
    /// derived from <paramref name="context"/>. Returns the rendered report string.
    /// </summary>
    public static string Render(string template, TemplateRenderContext context)
    {
        var result = template;

        result = result.Replace("{{story_title}}", context.StoryTitle ?? string.Empty);
        result = result.Replace("{{story_url}}", context.StoryUrl ?? string.Empty);
        result = result.Replace("{{run_date}}", FormatRunDate(context.Run));
        result = result.Replace("{{overall_result}}", FormatOverallResult(context.Run));
        result = result.Replace("{{scenarios}}", BuildScenariosTable(context.Scenarios, context.StepResults));
        result = result.Replace("{{logs}}", context.LogSection);
        result = result.Replace("{{screenshots}}", context.IncludeScreenshots ? BuildScreenshotsSection(context.Run) : string.Empty);
        result = result.Replace("{{ai_scenario_source}}", context.AiScenarioSource ?? string.Empty);
        result = result.Replace("{{timing_summary}}", BuildTimingSummary(context.Run, context.Scenarios, context.StepResults));

        return result;
    }

    // ─── Token value builders ────────────────────────────────────────────────

    private static string FormatRunDate(TestRun run)
    {
        var ts = run.CompletedAt ?? run.CreatedAt;
        return ts.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ");
    }

    private static string FormatOverallResult(TestRun run) =>
        run.Status == TestRunStatus.Completed ? "Passed" : "Failed";

    /// <summary>
    /// Builds a Markdown table with one row per scenario (AC-017).
    /// Columns: Scenario Name | Result | Step Count | Duration (ms)
    /// </summary>
    private static string BuildScenariosTable(
        IReadOnlyList<TestScenario> scenarios,
        IReadOnlyList<StepResult> stepResults)
    {
        if (scenarios.Count == 0)
            return string.Empty;

        var sb = new StringBuilder();
        sb.AppendLine("| Scenario Name | Result | Step Count | Duration (ms) |");
        sb.AppendLine("|---|---|---|---|");

        foreach (var scenario in scenarios)
        {
            var steps = stepResults.Where(s => s.ScenarioId == scenario.Id).ToList();
            var passed = steps.All(s => s.Status == StepStatus.Passed);
            var result = passed ? "Passed" : "Failed";
            var duration = steps.Sum(s => s.DurationMs);
            sb.AppendLine($"| {EscapeMarkdown(scenario.Title)} | {result} | {steps.Count} | {duration} |");
        }

        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// Returns a timing summary with total run duration and per-scenario timing.
    /// </summary>
    private static string BuildTimingSummary(
        TestRun run,
        IReadOnlyList<TestScenario> scenarios,
        IReadOnlyList<StepResult> stepResults)
    {
        var totalMs = stepResults.Sum(s => s.DurationMs);

        var sb = new StringBuilder();
        sb.AppendLine($"**Total Duration:** {totalMs} ms");
        sb.AppendLine();
        sb.AppendLine("**Per-Scenario Timing:**");
        sb.AppendLine();

        foreach (var scenario in scenarios)
        {
            var steps = stepResults.Where(s => s.ScenarioId == scenario.Id).ToList();
            var scenarioDuration = steps.Sum(s => s.DurationMs);
            sb.AppendLine($"- {EscapeMarkdown(scenario.Title)}: {scenarioDuration} ms");
        }

        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// Placeholder for screenshot content.
    /// Feature 0018 will populate actual screenshot blobs here.
    /// Until then, returns an empty string when no screenshots are available (AC-019).
    /// </summary>
    private static string BuildScreenshotsSection(TestRun run)
    {
        // Screenshots are populated by feature 0018 (PlaywrightExecutor screenshot capture).
        // This method returns an empty string until that feature is implemented.
        return string.Empty;
    }

    private static string EscapeMarkdown(string text) =>
        text.Replace("|", "\\|");
}
