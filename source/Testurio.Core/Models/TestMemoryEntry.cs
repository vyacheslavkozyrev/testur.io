namespace Testurio.Core.Models;

/// <summary>
/// A past test scenario stored in the <c>TestMemory</c> Cosmos DB container.
/// Used as few-shot context by generator agents (stage 4) and updated by the
/// FeedbackLoop (stage 7) and MemoryWriter (stage 8) pipeline stages.
/// Partition key: <see cref="UserId"/>.
/// </summary>
public class TestMemoryEntry
{
    /// <summary>UUID v4 document identifier.</summary>
    public string Id { get; init; } = Guid.NewGuid().ToString();

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

    /// <summary>Serialized scenario JSON produced by the generator agent for this story.</summary>
    public required string ScenarioText { get; init; }

    /// <summary>
    /// Quality signal in the range 0.0–1.0. Starts at 1.0 when written; updated by FeedbackLoop.
    /// Entries with <c>passRate &lt; 0.5</c> after <c>runCount &gt;= 5</c> are soft-deleted.
    /// </summary>
    public double PassRate { get; set; } = 1.0;

    /// <summary>Number of times this scenario has been reused across pipeline runs.</summary>
    public int RunCount { get; set; }

    /// <summary>ISO 8601 timestamp of the most recent reuse.</summary>
    public DateTimeOffset LastUsedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Soft-delete flag. Entries with <c>isDeleted: true</c> are excluded from retrieval.</summary>
    public bool IsDeleted { get; set; }
}
