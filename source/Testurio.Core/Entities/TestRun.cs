using Testurio.Core.Enums;

namespace Testurio.Core.Entities;

public class TestRun
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public required string ProjectId { get; init; }
    public required string UserId { get; init; }
    public required string JiraIssueKey { get; init; }
    public required string JiraIssueId { get; init; }
    public TestRunStatus Status { get; set; } = TestRunStatus.Pending;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public string? SkipReason { get; set; }
    public string? DeliveryError { get; set; }

    // ─── StoryParser metadata (stage 1 — feature 0025) ───────────────────────

    /// <summary>Parser mode used to produce the ParsedStory for this run. Null until the StoryParser stage completes.</summary>
    public ParserMode? ParserMode { get; set; }

    // ─── AgentRouter metadata (stage 2 — feature 0026) ───────────────────────

    /// <summary>
    /// Test types selected by the AgentRouter after classification and project-config filtering.
    /// Populated after stage 2 completes; empty array when the run was skipped due to no applicable type.
    /// Null until the AgentRouter stage completes.
    /// </summary>
    public string[]? ResolvedTestTypes { get; set; }

    /// <summary>
    /// Claude's brief rationale for the test-type classification decision.
    /// Populated after stage 2 completes; present on both successful and skipped paths.
    /// Null until the AgentRouter stage completes.
    /// </summary>
    public string? ClassificationReason { get; set; }

    // ─── Generator metadata (stage 4 — feature 0028) ─────────────────────────

    /// <summary>
    /// Warnings accumulated during the generator stage.
    /// Each entry describes one agent that exhausted its retry budget,
    /// e.g. <c>"api_test_generator: JSON parse failed after 4 attempts"</c>.
    /// Empty array when both agents complete successfully — never null.
    /// Populated by <c>TestRunJobProcessor</c> before invoking stage 5.
    /// </summary>
    public string[] GenerationWarnings { get; set; } = [];

    /// <summary>Blob Storage URI of the rendered report for this run. Populated by ReportWriterPlugin (feature 0009).</summary>
    public string? ReportBlobUri { get; set; }
    /// <summary>Warning recorded when the custom template blob could not be fetched and the built-in default was used instead.</summary>
    public string? ReportTemplateWarning { get; set; }
}
