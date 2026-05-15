namespace Testurio.Plugins.ReportWriterPlugin;

/// <summary>
/// Built-in default Markdown report template used when no custom template has been uploaded for the project.
/// Includes all supported placeholder tokens in a sensible layout (AC-030).
/// </summary>
public static class DefaultReportTemplate
{
    /// <summary>
    /// The default template content.  All supported tokens are present; the rendered output
    /// matches the format previously produced by the hard-coded ReportBuilderService.Build().
    /// </summary>
    public const string Content = """
        # Testurio Test Report — {{overall_result}}

        **Story:** [{{story_title}}]({{story_url}})
        **Run Date:** {{run_date}}

        ---

        ## Scenario Breakdown

        {{scenarios}}

        ---

        ## Timing Summary

        {{timing_summary}}

        ---

        ## AI Scenario Source

        {{ai_scenario_source}}

        ---

        ## Execution Logs

        {{logs}}

        ---

        ## Screenshots

        {{screenshots}}
        """;
}
