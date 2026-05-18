namespace Testurio.Core.Models;

/// <summary>
/// The outcome of executing a single step within a UI E2E scenario.
/// Stored as a nested array on each <see cref="ScenarioSummary"/> entry with
/// <c>TestType == "ui_e2e"</c>; <c>null</c> for <c>"api"</c> scenarios.
/// </summary>
/// <param name="StepIndex">1-based index of the step within the scenario's step list.</param>
/// <param name="Action">
/// Action discriminator — one of <c>"navigate"</c>, <c>"click"</c>, <c>"fill"</c>,
/// <c>"assert_visible"</c>, <c>"assert_text"</c>, or <c>"assert_url"</c>.
/// </param>
/// <param name="Passed"><c>true</c> when the step completed without error.</param>
/// <param name="ErrorMessage">
/// Error message when <paramref name="Passed"/> is <c>false</c>.
/// <c>"Skipped — preceding step failed"</c> for steps not reached due to an earlier failure.
/// <c>null</c> when <paramref name="Passed"/> is <c>true</c>.
/// </param>
/// <param name="ScreenshotBlobUri">
/// Full Blob Storage URI of the captured screenshot PNG, or <c>null</c>.
/// Non-null only for failed assertion steps where the Blob upload succeeded.
/// </param>
public record StepSummary(
    int StepIndex,
    string Action,
    bool Passed,
    string? ErrorMessage,
    string? ScreenshotBlobUri);

/// <summary>
/// Structured result for a single test scenario produced by the test executor.
/// Populated by <c>ReportWriter</c> (feature 0030) from <c>ExecutionResult</c> data.
/// </summary>
/// <param name="ScenarioId">Stable unique identifier for this scenario (UUID v4 or generator-assigned key).</param>
/// <param name="Title">Human-readable scenario title.</param>
/// <param name="Passed">Whether all assertions in this scenario passed.</param>
/// <param name="DurationMs">Total execution duration in milliseconds.</param>
/// <param name="ErrorSummary">Short error description when <paramref name="Passed"/> is <c>false</c>; otherwise <c>null</c>.</param>
/// <param name="TestType">
/// The test type that produced this scenario — <c>"api"</c> or <c>"ui_e2e"</c>.
/// Populated by <c>ReportWriter</c> when constructing from <c>ExecutionResult</c>.
/// </param>
/// <param name="ScreenshotUris">
/// Blob Storage URIs for screenshots captured during a failed <c>ui_e2e</c> scenario.
/// Empty list for API scenarios and passed UI scenarios.
/// Populated by <c>ReportWriter</c> from <c>UiE2eScenarioResult</c> step data.
/// </param>
/// <param name="Steps">
/// Per-step execution detail for <c>ui_e2e</c> scenarios; <c>null</c> for <c>api</c> scenarios.
/// Non-null, non-empty list with one entry per executed step for UI E2E scenarios.
/// Existing <c>TestResult</c> documents without this field deserialise to <c>null</c> gracefully.
/// </param>
public record ScenarioSummary(
    string ScenarioId,
    string Title,
    bool Passed,
    long DurationMs,
    string? ErrorSummary,
    string TestType,
    IReadOnlyList<string> ScreenshotUris,
    IReadOnlyList<StepSummary>? Steps = null);

/// <summary>
/// Structured AI-generated report produced by <c>ReportWriter</c> (feature 0030)
/// after all test scenarios have been executed.
/// Returned by Claude after analysing the <c>ExecutionResult</c> and used to
/// render the PM tool comment and persist the <c>TestResult</c> document.
/// </summary>
/// <param name="Verdict">
/// Overall verdict: <c>"PASSED"</c> if every scenario passed; <c>"FAILED"</c> otherwise.
/// Validated by <c>ReportWriter</c> against the raw <c>ExecutionResult</c> before acceptance.
/// </param>
/// <param name="Recommendation">
/// AI recommendation — exactly one of <c>"approve"</c>, <c>"request_fixes"</c>,
/// or <c>"flag_for_manual_review"</c>.
/// </param>
/// <param name="ScenarioSummaries">
/// Per-scenario summaries in execution order.
/// Contains only the scenario types that were executed (no phantom entries).
/// </param>
public record ReportContent(
    string Verdict,
    string Recommendation,
    IReadOnlyList<ScenarioSummary> ScenarioSummaries);
