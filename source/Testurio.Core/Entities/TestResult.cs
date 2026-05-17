using Testurio.Core.Models;

namespace Testurio.Core.Entities;

/// <summary>
/// Persisted outcome of a completed test run.
/// Written by <c>ReportWriter</c> (feature 0030) after all scenarios have been executed.
/// Read by the portal API (feature 0011) to serve history and run-detail views.
/// </summary>
public class TestResult
{
    public string Id { get; init; } = Guid.NewGuid().ToString();

    /// <summary>The <see cref="TestRun.Id"/> this result belongs to.</summary>
    public required string RunId { get; init; }

    public required string ProjectId { get; init; }

    public required string UserId { get; init; }

    /// <summary>Title extracted from the parsed user story.</summary>
    public required string StoryTitle { get; init; }

    /// <summary>Overall verdict: <c>"PASSED"</c> or <c>"FAILED"</c>.</summary>
    public required string Verdict { get; init; }

    /// <summary>
    /// AI-generated recommendation for this run.
    /// Typical values: <c>"approve"</c>, <c>"investigate"</c>, <c>"block"</c>.
    /// </summary>
    public required string Recommendation { get; init; }

    /// <summary>Total wall-clock duration of all scenario executions in milliseconds.</summary>
    public long TotalDurationMs { get; init; }

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>Soft-delete flag. Excluded from all read queries.</summary>
    public bool IsDeleted { get; set; }

    // ─── Feature 0011 fields ──────────────────────────────────────────────────

    /// <summary>
    /// Per-scenario execution results.
    /// Populated by <c>ReportWriter</c> from <c>ReportContent.ScenarioSummaries</c>
    /// before calling <c>ITestResultRepository.SaveAsync</c>.
    /// </summary>
    public IReadOnlyList<ScenarioSummary> ScenarioResults { get; init; } =
        Array.Empty<ScenarioSummary>();

    /// <summary>
    /// The full markdown text posted as a comment to the originating ADO/Jira ticket.
    /// <c>null</c> when report delivery has not yet completed or was skipped.
    /// Populated by <c>ReportWriter</c> alongside <see cref="ScenarioResults"/>.
    /// </summary>
    public string? RawCommentMarkdown { get; init; }
}
