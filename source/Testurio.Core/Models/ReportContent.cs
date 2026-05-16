namespace Testurio.Core.Models;

/// <summary>
/// Structured result for a single test scenario produced by the test executor.
/// Populated by <c>ReportWriter</c> (feature 0030) from <c>ExecutionResult</c> data.
/// </summary>
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
public record ScenarioSummary(
    string Title,
    bool Passed,
    long DurationMs,
    string? ErrorSummary,
    string TestType,
    IReadOnlyList<string> ScreenshotUris);
