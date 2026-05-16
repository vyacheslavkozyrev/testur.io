namespace Testurio.Core.Models;

/// <summary>
/// A single row in the per-project run history table.
/// Projected from <c>TestResult</c> by <c>StatsRepository.GetProjectHistoryAsync</c>.
/// </summary>
/// <param name="Id">The <c>TestResult</c> document id.</param>
/// <param name="RunId">The originating <c>TestRun</c> id.</param>
/// <param name="StoryTitle">Title extracted from the parsed user story.</param>
/// <param name="Verdict">Overall verdict: <c>"PASSED"</c> or <c>"FAILED"</c>.</param>
/// <param name="Recommendation">AI recommendation: e.g. <c>"approve"</c>, <c>"investigate"</c>, <c>"block"</c>.</param>
/// <param name="TotalApiScenarios">Total number of API scenarios executed in this run.</param>
/// <param name="PassedApiScenarios">Number of API scenarios that passed.</param>
/// <param name="TotalUiE2eScenarios">Total number of UI E2E scenarios executed in this run.</param>
/// <param name="PassedUiE2eScenarios">Number of UI E2E scenarios that passed.</param>
/// <param name="TotalDurationMs">Total wall-clock execution duration in milliseconds.</param>
/// <param name="CreatedAt">UTC timestamp when the test result was persisted.</param>
public record RunHistoryItem(
    string Id,
    string RunId,
    string StoryTitle,
    string Verdict,
    string Recommendation,
    int TotalApiScenarios,
    int PassedApiScenarios,
    int TotalUiE2eScenarios,
    int PassedUiE2eScenarios,
    long TotalDurationMs,
    DateTimeOffset CreatedAt);
