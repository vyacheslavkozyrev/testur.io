using Testurio.Core.Models;

namespace Testurio.Core.Interfaces;

/// <summary>
/// Contract for Cosmos DB access to the <c>TestMemory</c> container.
/// Abstracts the DiskANN vector search behind an interface so that pipeline projects
/// can depend on <c>Testurio.Core</c> only and remain infrastructure-ignorant.
/// </summary>
public interface ITestMemoryRepository
{
    /// <summary>
    /// Retrieves the top-3 most semantically similar test memory entries for the given
    /// <paramref name="userId"/> and <paramref name="projectId"/>, ordered by cosine
    /// similarity (highest first). Only entries with <c>isDeleted = false</c> are returned.
    /// </summary>
    /// <param name="userId">Partition key — isolates results to the requesting user.</param>
    /// <param name="projectId">Project identifier — further scopes results to the current project.</param>
    /// <param name="embedding">Story embedding vector (1536 dimensions) for similarity search.</param>
    /// <param name="cancellationToken">Cancellation token forwarded to Cosmos SDK calls.</param>
    /// <returns>Up to 3 matching <see cref="TestMemoryEntry"/> instances, never null.</returns>
    Task<IReadOnlyList<TestMemoryEntry>> FindSimilarAsync(
        string userId,
        string projectId,
        float[] embedding,
        CancellationToken cancellationToken = default);
}
