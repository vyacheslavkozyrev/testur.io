namespace Testurio.Core.Models;

/// <summary>
/// Output of the MemoryRetrieval pipeline stage (stage 3).
/// Contains the semantically similar past test scenarios retrieved from the <c>TestMemory</c>
/// Cosmos DB container. Passed to each generator agent in stage 4 as few-shot context.
/// </summary>
public sealed record MemoryRetrievalResult
{
    /// <summary>
    /// Top-k past scenarios retrieved by vector similarity search, ordered by cosine similarity
    /// (highest first). Empty when no matching scenarios exist (cold start) or when retrieval
    /// failed gracefully.
    /// </summary>
    public required IReadOnlyList<TestMemoryEntry> Scenarios { get; init; }
}
