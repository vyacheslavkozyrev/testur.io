using Microsoft.Azure.Cosmos;
using Testurio.Core.Models;

namespace Testurio.Infrastructure.Cosmos;

/// <summary>
/// Cosmos DB repository for the <c>TestMemory</c> container.
/// Executes a DiskANN vector search scoped to <c>userId + projectId</c>, filtered
/// to non-deleted entries, and returns the top-3 entries ordered by cosine similarity.
/// Partition key: <c>userId</c>.
/// </summary>
public class TestMemoryRepository
{
    private readonly Container _container;

    public TestMemoryRepository(CosmosClient cosmosClient, string databaseName)
    {
        _container = cosmosClient.GetContainer(databaseName, "TestMemory");
    }

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
    public async Task<IReadOnlyList<TestMemoryEntry>> FindSimilarAsync(
        string userId,
        string projectId,
        float[] embedding,
        CancellationToken cancellationToken = default)
    {
        // DiskANN vector search: VectorDistance returns cosine distance (lower = more similar),
        // so ORDER BY ascending gives the most similar entries first.
        // We project the score but filter it out when deserialising by using a wrapper.
        var query = new QueryDefinition(
            """
            SELECT TOP 3 c.id, c.userId, c.projectId, c.testType,
                         c.storyText, c.scenarioText, c.passRate,
                         c.runCount, c.lastUsedAt, c.isDeleted
            FROM c
            WHERE c.userId     = @userId
              AND c.projectId  = @projectId
              AND c.isDeleted  = false
            ORDER BY VectorDistance(c.storyEmbedding, @embedding)
            """)
            .WithParameter("@userId", userId)
            .WithParameter("@projectId", projectId)
            .WithParameter("@embedding", embedding);

        var results = new List<TestMemoryEntry>();

        using var iterator = _container.GetItemQueryIterator<TestMemoryEntry>(
            query,
            requestOptions: new QueryRequestOptions
            {
                PartitionKey = new PartitionKey(userId)
            });

        while (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync(cancellationToken);
            results.AddRange(page);
        }

        return results.AsReadOnly();
    }
}
