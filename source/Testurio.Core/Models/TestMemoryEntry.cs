namespace Testurio.Core.Models;

/// <summary>
/// A past test scenario stored in the <c>TestMemory</c> Cosmos DB container.
/// Used as few-shot context by generator agents (stage 4) and updated by the
/// FeedbackLoop (stage 7) and MemoryWriter (stage 8) pipeline stages.
/// Partition key: <see cref="UserId"/>.
/// </summary>
public sealed class TestMemoryEntry
{
    /// <summary>UUID v4 document identifier.</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>Azure AD B2C OID — partition key.</summary>
    public required string UserId { get; init; }

    /// <summary>
    /// Project UUID. Null for cross-project shared memory entries
    /// (cross-project sharing is post-MVP, feature 0039).
    /// </summary>
    public string? ProjectId { get; init; }

    /// <summary>Test type that produced this scenario: <c>api</c> or <c>ui_e2e</c>.</summary>
    public required string TestType { get; init; }

    /// <summary>Original parsed story text used for vector similarity search.</summary>
    public required string StoryText { get; init; }

    /// <summary>
    /// Serialized scenario JSON produced by the generator agent for this story.
    /// <c>null</c> for QA-lead feedback entries (<see cref="Source"/> = <c>"qalead"</c>)
    /// where no scenario JSON exists yet.
    /// </summary>
    public string? ScenarioText { get; init; }

    /// <summary>
    /// Story embedding vector (1536 dimensions) produced by Azure OpenAI <c>text-embedding-3-small</c>.
    /// Written by MemoryWriter (stage 8) and FeedbackLoop (stage 7), used as the DiskANN index field.
    /// </summary>
    public float[]? StoryEmbedding { get; init; }

    /// <summary>
    /// Quality signal in the range 0.0–1.0. Starts at 1.0 when written; updated by FeedbackLoop.
    /// Entries with <c>passRate &lt; 0.5</c> after <c>runCount &gt;= 5</c> are soft-deleted.
    /// Omitted (<c>null</c>) for QA-lead feedback entries (<see cref="Source"/> = <c>"qalead"</c>).
    /// </summary>
    public double? PassRate { get; set; }

    /// <summary>
    /// Number of times this scenario has been reused across pipeline runs.
    /// Omitted (<c>null</c>) for QA-lead feedback entries (<see cref="Source"/> = <c>"qalead"</c>).
    /// </summary>
    public int? RunCount { get; set; }

    /// <summary>ISO 8601 timestamp of the most recent reuse.</summary>
    public DateTimeOffset LastUsedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Soft-delete flag. Entries with <c>isDeleted: true</c> are excluded from retrieval.</summary>
    public bool IsDeleted { get; set; }

    // ─── Fields added by feature 0031 (FeedbackLoop) ─────────────────────────

    /// <summary>
    /// Origin of this memory entry.
    /// <c>"pipeline"</c> — written by MemoryWriter (stage 8) from a successful test run.
    /// <c>"qalead"</c>  — written by FeedbackLoop (stage 7) from a QA-lead comment with <c>@testurio memorize</c>.
    /// <c>null</c> for entries created before feature 0031 (treated as <c>"pipeline"</c>).
    /// </summary>
    public string? Source { get; init; }

    /// <summary>
    /// Work item / issue identifier associated with this entry.
    /// Populated by FeedbackLoop; <c>null</c> for pipeline-written entries.
    /// </summary>
    public string? WorkItemId { get; init; }

    /// <summary>
    /// PM-tool-assigned comment identifier for the comment that triggered this entry.
    /// Populated by FeedbackLoop; <c>null</c> for pipeline-written entries.
    /// </summary>
    public string? CommentId { get; init; }

    /// <summary>
    /// UTC timestamp of the most recent upsert of this document.
    /// Set by FeedbackLoop on every write; <c>null</c> for pipeline-written entries.
    /// </summary>
    public DateTimeOffset? UpdatedAt { get; set; }

    /// <summary>UTC timestamp when the document was first created. Preserved on upsert.</summary>
    public DateTimeOffset? CreatedAt { get; init; }
}
