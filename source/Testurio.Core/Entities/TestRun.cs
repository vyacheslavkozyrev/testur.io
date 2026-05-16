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

    /// <summary>
    /// How the story was parsed in stage 1.
    /// Null until the StoryParser stage completes.
    /// </summary>
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
}
