using Testurio.Core.Models;
using Testurio.Pipeline.ReportWriter;

namespace Testurio.UnitTests.Pipeline.ReportWriter;

/// <summary>
/// Unit tests for <see cref="PmCommentFormatter"/> covering all formatting paths defined
/// in feature 0030 acceptance criteria (AC-009 through AC-012).
/// Pure function — no mocking required.
/// </summary>
public class PmCommentFormatterTests
{
    private const string StoryTitle = "User can log in with valid credentials";

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private static ScenarioSummary MakeApiSummary(
        string id, string title, bool passed,
        long durationMs = 150, string? errorSummary = null) =>
        new(id, title, passed, durationMs, errorSummary, "api", []);

    private static ScenarioSummary MakeUiSummary(
        string id, string title, bool passed,
        long durationMs = 300, string? errorSummary = null,
        IReadOnlyList<string>? screenshots = null) =>
        new(id, title, passed, durationMs, errorSummary, "ui_e2e", screenshots ?? []);

    // ─── AC-009: verdict line ──────────────────────────────────────────────────

    [Fact]
    public void Format_PassedVerdict_StartsWithGreenCheckAndPassed()
    {
        var report = new ReportContent("PASSED", "approve", [
            MakeApiSummary("sc1", "Login API", true)
        ]);

        var result = PmCommentFormatter.Format(report, StoryTitle);

        Assert.StartsWith($"✅ **PASSED** ({StoryTitle})", result);
    }

    [Fact]
    public void Format_FailedVerdict_StartsWithRedCrossAndFailed()
    {
        var report = new ReportContent("FAILED", "request_fixes", [
            MakeApiSummary("sc1", "Login API", false, 200, "Expected: 200 / Actual: 401")
        ]);

        var result = PmCommentFormatter.Format(report, StoryTitle);

        Assert.StartsWith($"❌ **FAILED** ({StoryTitle})", result);
    }

    // ─── AC-010: per-scenario section ─────────────────────────────────────────

    [Fact]
    public void Format_PassedScenario_RendersGreenCheckAndDuration()
    {
        var report = new ReportContent("PASSED", "approve", [
            MakeApiSummary("sc1", "GET /users returns 200", true, 123)
        ]);

        var result = PmCommentFormatter.Format(report, StoryTitle);

        Assert.Contains("✅ GET /users returns 200 — 123 ms", result);
    }

    [Fact]
    public void Format_FailedApiScenario_RendersRedCrossAndAssertionDiff()
    {
        var errorSummary = "Expected: 200 / Actual: 401";
        var report = new ReportContent("FAILED", "request_fixes", [
            MakeApiSummary("sc1", "GET /users returns 200", false, 200, errorSummary)
        ]);

        var result = PmCommentFormatter.Format(report, StoryTitle);

        Assert.Contains("❌ GET /users returns 200 — 200 ms", result);
        Assert.Contains("Expected: 200 / Actual: 401", result);
    }

    [Fact]
    public void Format_FailedUiE2eScenario_RendersStepError()
    {
        var errorSummary = "Step 2: Element '#submit-btn' not found";
        var report = new ReportContent("FAILED", "request_fixes", [
            MakeUiSummary("sc1", "Login flow", false, 800, errorSummary)
        ]);

        var result = PmCommentFormatter.Format(report, StoryTitle);

        Assert.Contains("❌ Login flow — 800 ms", result);
        Assert.Contains("Step 2: Element '#submit-btn' not found", result);
    }

    // ─── AC-011: screenshot links ──────────────────────────────────────────────

    [Fact]
    public void Format_FailedUiE2eWithScreenshot_RendersClickableLink()
    {
        const string screenshotUri = "https://blob.example.com/screenshots/run1/step2.png";
        var report = new ReportContent("FAILED", "flag_for_manual_review", [
            MakeUiSummary("sc1", "Login flow", false, 800,
                "Step 2 failed", [screenshotUri])
        ]);

        var result = PmCommentFormatter.Format(report, StoryTitle);

        Assert.Contains($"[View screenshot]({screenshotUri})", result);
    }

    [Fact]
    public void Format_PassedUiE2eWithNoScreenshots_DoesNotRenderScreenshotLink()
    {
        var report = new ReportContent("PASSED", "approve", [
            MakeUiSummary("sc1", "Login flow", true, 800)
        ]);

        var result = PmCommentFormatter.Format(report, StoryTitle);

        Assert.DoesNotContain("[View screenshot]", result);
    }

    // ─── AC-012: recommendation line ──────────────────────────────────────────

    [Theory]
    [InlineData("approve", "Approve and merge")]
    [InlineData("request_fixes", "Request fixes")]
    [InlineData("flag_for_manual_review", "Flag for manual review")]
    public void Format_RecommendationLabels_AreRenderedCorrectly(
        string recommendation, string expectedLabel)
    {
        var verdict = recommendation == "approve" ? "PASSED" : "FAILED";
        var report = new ReportContent(verdict, recommendation, [
            MakeApiSummary("sc1", "Any scenario", recommendation == "approve")
        ]);

        var result = PmCommentFormatter.Format(report, StoryTitle);

        Assert.Contains($"**Recommendation:** {expectedLabel}", result);
    }

    // ─── Edge cases ────────────────────────────────────────────────────────────

    [Fact]
    public void Format_EmptyScenarioList_DoesNotRenderScenariosSection()
    {
        var report = new ReportContent("PASSED", "approve", []);

        var result = PmCommentFormatter.Format(report, StoryTitle);

        Assert.DoesNotContain("**Scenarios:**", result);
    }

    [Fact]
    public void Format_OnlyApiScenarios_DoesNotContainUiE2eMarkers()
    {
        var report = new ReportContent("PASSED", "approve", [
            MakeApiSummary("sc1", "POST /login", true, 100),
            MakeApiSummary("sc2", "GET /profile", true, 80),
        ]);

        var result = PmCommentFormatter.Format(report, StoryTitle);

        Assert.Contains("POST /login", result);
        Assert.Contains("GET /profile", result);
        Assert.DoesNotContain("[View screenshot]", result);
    }

    [Fact]
    public void Format_OnlyUiE2eScenarios_DoesNotContainApiMarkers()
    {
        var report = new ReportContent("PASSED", "approve", [
            MakeUiSummary("sc1", "Full login flow", true, 1500),
        ]);

        var result = PmCommentFormatter.Format(report, StoryTitle);

        Assert.Contains("Full login flow", result);
        Assert.DoesNotContain("[View screenshot]", result);
    }

    // ─── Feature 0018: per-step Markdown sub-table ────────────────────────────

    [Fact]
    public void Format_PassedUiE2eWithSteps_NoStepSubTable()
    {
        // AC-027: PASSED UI E2E scenario — no step sub-table rendered.
        var steps = new List<StepSummary>
        {
            new(1, "navigate", true, null, null),
            new(2, "click", true, null, null),
        }.AsReadOnly();

        var summary = new ScenarioSummary("sc1", "Login flow", true, 800,
            null, "ui_e2e", [], steps);
        var report = new ReportContent("PASSED", "approve", [summary]);

        var result = PmCommentFormatter.Format(report, StoryTitle);

        // Passed UI E2E — no step sub-table generated.
        Assert.DoesNotContain("| # |", result);
        Assert.DoesNotContain("navigate", result);
    }

    [Fact]
    public void Format_FailedUiE2eWithFailedAssertionStep_SubTableContainsErrorAndScreenshotLink()
    {
        // AC-028/AC-029: failed assertion step — error message and screenshot link in table.
        const string screenshotUri = "https://blob.example.com/step3.png";
        var steps = new List<StepSummary>
        {
            new(1, "navigate", true, null, null),
            new(2, "fill", true, null, null),
            new(3, "assert_text", false, "Expected 'Welcome' but got 'Error'", screenshotUri),
        }.AsReadOnly();

        var summary = new ScenarioSummary("sc1", "Login flow", false, 800,
            "Step 3: assert_text failed", "ui_e2e", [screenshotUri], steps);
        var report = new ReportContent("FAILED", "request_fixes", [summary]);

        var result = PmCommentFormatter.Format(report, StoryTitle);

        Assert.Contains("| # |", result);
        Assert.Contains("`assert_text`", result);
        Assert.Contains("Expected 'Welcome' but got 'Error'", result);
        Assert.Contains($"[Screenshot]({screenshotUri})", result);
    }

    [Fact]
    public void Format_FailedUiE2eWithSkippedSteps_SkippedRowsRenderedInTable()
    {
        // AC-028: skipped steps are rendered in the sub-table.
        var steps = new List<StepSummary>
        {
            new(1, "click", false, "Element '#btn' not found", null),
            new(2, "fill", false, "Skipped — preceding step failed", null),
            new(3, "assert_visible", false, "Skipped — preceding step failed", null),
        }.AsReadOnly();

        var summary = new ScenarioSummary("sc1", "Checkout flow", false, 600,
            "Step 1 failed", "ui_e2e", [], steps);
        var report = new ReportContent("FAILED", "request_fixes", [summary]);

        var result = PmCommentFormatter.Format(report, StoryTitle);

        Assert.Contains("Skipped — preceding step failed", result);
        Assert.Contains("`click`", result);
        Assert.Contains("`fill`", result);
        Assert.Contains("`assert_visible`", result);
    }

    [Fact]
    public void Format_ApiScenarioInSameRun_NoStepSubTable()
    {
        // AC-030: API scenarios in the same run must not produce a step sub-table.
        var apiSummary = MakeApiSummary("a1", "POST /checkout", false, 200, "Expected: 200 / Actual: 500");
        var steps = new List<StepSummary>
        {
            new(1, "navigate", false, "Connection refused", null),
        }.AsReadOnly();
        var uiSummary = new ScenarioSummary("u1", "Checkout flow", false, 600,
            "Step 1 failed", "ui_e2e", [], steps);

        var report = new ReportContent("FAILED", "request_fixes", [apiSummary, uiSummary]);

        var result = PmCommentFormatter.Format(report, StoryTitle);

        // The API scenario (apiSummary) must NOT have a step table.
        // The UI E2E scenario (uiSummary) MUST have a step table.
        Assert.Contains("| # |", result);  // UI E2E step table present
        Assert.Contains("`navigate`", result);

        // Verify only one table header appears (from the single ui_e2e scenario).
        var tableHeaderCount = result.Split("| # |").Length - 1;
        Assert.Equal(1, tableHeaderCount);
    }

    [Fact]
    public void Format_UiE2eWithNullSteps_NoStepSubTable()
    {
        // AC-015: null steps (pre-feature documents) — no sub-table rendered.
        var summary = new ScenarioSummary("sc1", "Legacy flow", false, 400,
            "Unknown error", "ui_e2e", [], null);
        var report = new ReportContent("FAILED", "request_fixes", [summary]);

        var result = PmCommentFormatter.Format(report, StoryTitle);

        Assert.DoesNotContain("| # |", result);
    }

    [Fact]
    public void Format_OnlyApiRun_NoStepSubTable()
    {
        // AC-031: api-only run — step sub-section absent entirely.
        var report = new ReportContent("PASSED", "approve", [
            MakeApiSummary("a1", "GET /users", true),
            MakeApiSummary("a2", "POST /users", true),
        ]);

        var result = PmCommentFormatter.Format(report, StoryTitle);

        Assert.DoesNotContain("| # |", result);
    }
}
