using System.Text;
using Testurio.Core.Models;

namespace Testurio.Pipeline.ReportWriter;

/// <summary>
/// Renders a formatted markdown comment from a <see cref="ReportContent"/> and story title
/// suitable for posting to ADO or Jira.
/// Stateless static helper — no I/O, no dependencies.
/// </summary>
public static class PmCommentFormatter
{
    private static readonly Dictionary<string, string> RecommendationLabels =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["approve"]                 = "Approve and merge",
            ["request_fixes"]           = "Request fixes",
            ["flag_for_manual_review"]  = "Flag for manual review",
        };

    /// <summary>
    /// Renders the full PM tool comment markdown.
    /// </summary>
    /// <param name="report">The structured report content produced by Claude.</param>
    /// <param name="storyTitle">The parsed story title shown in the verdict header.</param>
    /// <returns>A multi-line markdown string ready to post as a ticket comment.</returns>
    public static string Format(ReportContent report, string storyTitle)
    {
        var sb = new StringBuilder();

        // AC-009: verdict line with story title.
        var verdictIcon = report.Verdict == "PASSED" ? "✅" : "❌";
        sb.AppendLine($"{verdictIcon} **{report.Verdict}** ({storyTitle})");
        sb.AppendLine();

        // AC-010: per-scenario section.
        if (report.ScenarioSummaries.Count > 0)
        {
            sb.AppendLine("**Scenarios:**");
            sb.AppendLine();

            foreach (var scenario in report.ScenarioSummaries)
            {
                var icon = scenario.Passed ? "✅" : "❌";
                sb.AppendLine($"{icon} {scenario.Title} — {scenario.DurationMs} ms");

                if (!scenario.Passed && !string.IsNullOrWhiteSpace(scenario.ErrorSummary))
                {
                    // AC-010: assertion diffs or step errors as an indented block.
                    foreach (var line in scenario.ErrorSummary.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                        sb.AppendLine($"  > {line.Trim()}");
                }

                // AC-011: screenshot links for failed UI E2E scenarios.
                if (!scenario.Passed && scenario.ScreenshotUris.Count > 0)
                {
                    foreach (var uri in scenario.ScreenshotUris)
                        sb.AppendLine($"  [View screenshot]({uri})");
                }
            }

            sb.AppendLine();
        }

        // AC-012: recommendation line.
        var recommendationLabel = RecommendationLabels.TryGetValue(report.Recommendation, out var label)
            ? label
            : report.Recommendation;

        sb.Append($"**Recommendation:** {recommendationLabel}");

        return sb.ToString();
    }
}
