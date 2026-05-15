using Testurio.Core.Entities;
using Testurio.Core.Enums;
using Testurio.Plugins.ReportWriterPlugin;

namespace Testurio.UnitTests.Plugins;

public class TemplateRendererTests
{
    private static TestRun MakeRun(TestRunStatus status = TestRunStatus.Completed) => new()
    {
        Id = "run1",
        ProjectId = "proj1",
        UserId = "user1",
        JiraIssueKey = "PROJ-1",
        JiraIssueId = "10001",
        Status = status,
        CompletedAt = new DateTimeOffset(2026, 5, 7, 12, 0, 0, TimeSpan.Zero),
    };

    private static TestScenario MakeScenario(string id, string title) => new()
    {
        Id = id,
        TestRunId = "run1",
        ProjectId = "proj1",
        UserId = "user1",
        Title = title,
        Steps = [],
    };

    private static StepResult MakeStep(
        string scenarioId,
        StepStatus status,
        long durationMs = 100) => new()
    {
        ScenarioId = scenarioId,
        TestRunId = "run1",
        ProjectId = "proj1",
        UserId = "user1",
        StepTitle = "Step",
        ExpectedStatusCode = 200,
        ActualStatusCode = 200,
        DurationMs = durationMs,
        Status = status,
    };

    private static TemplateRenderContext MakeContext(
        TestRun? run = null,
        IReadOnlyList<TestScenario>? scenarios = null,
        IReadOnlyList<StepResult>? stepResults = null,
        string logSection = "",
        bool includeScreenshots = false,
        string? storyTitle = null,
        string? storyUrl = null) => new(
            Run: run ?? MakeRun(),
            Scenarios: scenarios ?? [],
            StepResults: stepResults ?? [],
            LogSection: logSection,
            IncludeScreenshots: includeScreenshots,
            StoryTitle: storyTitle,
            StoryUrl: storyUrl);

    // ─── overall_result ──────────────────────────────────────────────────────

    [Fact]
    public void Render_ExpandsOverallResult_ToPassedWhenCompleted()
    {
        var result = TemplateRenderer.Render("{{overall_result}}", MakeContext(MakeRun(TestRunStatus.Completed)));
        Assert.Equal("Passed", result);
    }

    [Fact]
    public void Render_ExpandsOverallResult_ToFailedWhenFailed()
    {
        var result = TemplateRenderer.Render("{{overall_result}}", MakeContext(MakeRun(TestRunStatus.Failed)));
        Assert.Equal("Failed", result);
    }

    // ─── story metadata ───────────────────────────────────────────────────────

    [Fact]
    public void Render_ExpandsStoryTitle_WhenProvided()
    {
        var result = TemplateRenderer.Render("{{story_title}}", MakeContext(storyTitle: "My Story"));
        Assert.Equal("My Story", result);
    }

    [Fact]
    public void Render_ExpandsStoryTitle_ToEmptyWhenNull()
    {
        var result = TemplateRenderer.Render("{{story_title}}", MakeContext(storyTitle: null));
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void Render_ExpandsStoryUrl_WhenProvided()
    {
        var result = TemplateRenderer.Render("{{story_url}}", MakeContext(storyUrl: "https://jira.example.com/browse/PROJ-1"));
        Assert.Equal("https://jira.example.com/browse/PROJ-1", result);
    }

    // ─── run_date ─────────────────────────────────────────────────────────────

    [Fact]
    public void Render_ExpandsRunDate_InIso8601UtcFormat()
    {
        var result = TemplateRenderer.Render("{{run_date}}", MakeContext());
        // CompletedAt = 2026-05-07T12:00:00Z
        Assert.Equal("2026-05-07T12:00:00Z", result);
    }

    // ─── scenarios table ──────────────────────────────────────────────────────

    [Fact]
    public void Render_ExpandsScenarios_ToMarkdownTable()
    {
        var scenario = MakeScenario("s1", "Login Flow");
        var step = MakeStep("s1", StepStatus.Passed, 150);
        var context = MakeContext(scenarios: [scenario], stepResults: [step]);

        var result = TemplateRenderer.Render("{{scenarios}}", context);

        Assert.Contains("| Scenario Name |", result);
        Assert.Contains("Login Flow", result);
        Assert.Contains("Passed", result);
    }

    [Fact]
    public void Render_ExpandsScenarios_ToEmptyStringWhenNoScenarios()
    {
        var result = TemplateRenderer.Render("{{scenarios}}", MakeContext());
        Assert.Equal(string.Empty, result);
    }

    // ─── logs ────────────────────────────────────────────────────────────────

    [Fact]
    public void Render_ExpandsLogs_WhenLogSectionProvided()
    {
        var context = MakeContext(logSection: "Step 1: GET /api/health");
        var result = TemplateRenderer.Render("{{logs}}", context);
        Assert.Equal("Step 1: GET /api/health", result);
    }

    [Fact]
    public void Render_ExpandsLogs_ToEmptyWhenLogSectionEmpty()
    {
        var result = TemplateRenderer.Render("{{logs}}", MakeContext(logSection: string.Empty));
        Assert.Equal(string.Empty, result);
    }

    // ─── screenshots ──────────────────────────────────────────────────────────

    [Fact]
    public void Render_ExpandsScreenshots_ToEmptyString_WhenIncludeScreenshotsFalse()
    {
        var result = TemplateRenderer.Render("{{screenshots}}", MakeContext(includeScreenshots: false));
        Assert.Equal(string.Empty, result);
    }

    // ─── case sensitivity (AC-020) ────────────────────────────────────────────

    [Fact]
    public void Render_LeavesUnrecognisedCasing_AsIs()
    {
        var result = TemplateRenderer.Render("{{Overall_Result}}", MakeContext());
        // AC-020: case-sensitive — {{Overall_Result}} is not a supported token.
        Assert.Equal("{{Overall_Result}}", result);
    }

    // ─── unknown tokens left as-is (AC-020, AC-035) ──────────────────────────

    [Fact]
    public void Render_LeavesUnknownTokens_AsIs()
    {
        var result = TemplateRenderer.Render("{{author}}", MakeContext());
        Assert.Equal("{{author}}", result);
    }

    // ─── missing data replaced with empty string (AC-021) ────────────────────

    [Fact]
    public void Render_ReplacesTokenWithEmptyString_WhenDataMissing()
    {
        // ai_scenario_source not provided — should become empty string.
        var result = TemplateRenderer.Render("{{ai_scenario_source}}", MakeContext());
        Assert.Equal(string.Empty, result);
    }

    // ─── timing_summary ──────────────────────────────────────────────────────

    [Fact]
    public void Render_ExpandsTimingSummary_WithTotalAndPerScenarioDuration()
    {
        var scenario = MakeScenario("s1", "Checkout");
        var step = MakeStep("s1", StepStatus.Passed, 300);
        var context = MakeContext(scenarios: [scenario], stepResults: [step]);

        var result = TemplateRenderer.Render("{{timing_summary}}", context);

        Assert.Contains("300 ms", result);
        Assert.Contains("Checkout", result);
    }
}
