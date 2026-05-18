using Microsoft.Azure.Cosmos;
using Testurio.Core.Interfaces;
using Testurio.Core.Models;

namespace Testurio.Infrastructure.Cosmos;

/// <summary>
/// Cosmos DB repository for the <c>TestMemory</c> container.
/// Executes a DiskANN vector search scoped to <c>userId + projectId</c>, filtered
/// to non-deleted entries, and returns the top-3 entries ordered by cosine similarity.
/// Partition key: <c>userId</c>.
/// </summary>
public class TestMemoryRepository : ITestMemoryRepository
{
    private readonly Container _container = null!;

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
        // Cosmos DiskANN vector search: VectorDistance ORDER BY cannot be combined with
        // arbitrary WHERE predicates on non-vector fields. We scope to the partition key via
        // QueryRequestOptions (which enforces userId isolation at the SDK level) and filter
        // projectId and isDeleted client-side from the ranked results.
        // We fetch a small over-fetch (TOP 10) to ensure up to 3 non-deleted, in-project entries
        // survive the client-side filter.
        var query = new QueryDefinition(
            """
            SELECT TOP 10 c.id, c.userId, c.projectId, c.testType,
                          c.storyText, c.scenarioText, c.passRate,
                          c.runCount, c.lastUsedAt, c.isDeleted
            FROM c
            ORDER BY VectorDistance(c.storyEmbedding, @embedding)
            """)
            .WithParameter("@embedding", embedding);

        var rawResults = new List<TestMemoryEntry>();

        using var iterator = _container.GetItemQueryIterator<TestMemoryEntry>(
            query,
            requestOptions: new QueryRequestOptions
            {
                PartitionKey = new PartitionKey(userId)
            });

        while (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync(cancellationToken);
            rawResults.AddRange(page);
        }

        // Apply projectId scope and isDeleted filter client-side, then take top 3.
        var results = rawResults
            .Where(e => e.ProjectId == projectId && !e.IsDeleted)
            .Take(3)
            .ToList();

        return results.AsReadOnly();
    }

    /// <summary>
    /// Upserts a QA-lead feedback entry to the <c>TestMemory</c> container (feature 0031).
    /// Upsert key: <paramref name="workItemId"/> + <paramref name="testType"/> + <c>source="qalead"</c>
    /// within the <paramref name="userId"/> partition.
    /// Preserves <c>id</c> and <c>createdAt</c> on update; sets <c>updatedAt = UtcNow</c> always.
    /// <c>passRate</c> and <c>runCount</c> are omitted — qalead entries are not subject to the
    /// quality-loop soft-delete.
    /// </summary>
    public async Task UpsertFeedbackAsync(
        string userId,
        Guid projectId,
        string testType,
        string feedbackText,
        float[] storyEmbedding,
        string workItemId,
        string commentId,
        CancellationToken cancellationToken = default)
    {
        // Query for an existing qalead entry with the same workItemId + testType within this partition.
        var lookup = new QueryDefinition(
            """
            SELECT c.id, c.createdAt
            FROM c
            WHERE c.workItemId = @workItemId
              AND c.testType   = @testType
              AND c.source     = 'qalead'
            """)
            .WithParameter("@workItemId", workItemId)
            .WithParameter("@testType", testType);

        string? existingId = null;
        DateTimeOffset? existingCreatedAt = null;

        using var lookupIterator = _container.GetItemQueryIterator<ExistingEntry>(
            lookup,
            requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(userId) });

        while (lookupIterator.HasMoreResults)
        {
            var page = await lookupIterator.ReadNextAsync(cancellationToken);
            var first = page.FirstOrDefault();
            if (first is not null)
            {
                existingId = first.Id;
                existingCreatedAt = first.CreatedAt;
                break;
            }
        }

        var now = DateTimeOffset.UtcNow;

        var document = new TestMemoryEntry
        {
            // Preserve id and createdAt on update; generate on insert.
            Id = existingId ?? Guid.NewGuid().ToString(),
            UserId = userId,
            ProjectId = projectId.ToString(),
            TestType = testType,
            StoryText = feedbackText,
            ScenarioText = null,
            StoryEmbedding = storyEmbedding,
            Source = "qalead",
            WorkItemId = workItemId,
            CommentId = commentId,
            IsDeleted = false,
            // PassRate and RunCount are intentionally omitted (null) for qalead entries.
            PassRate = null,
            RunCount = null,
            LastUsedAt = now,
            CreatedAt = existingCreatedAt ?? now,
            UpdatedAt = now,
        };

        await _container.UpsertItemAsync(
            document,
            new PartitionKey(userId),
            cancellationToken: cancellationToken);
    }

    // Minimal projection type used by the lookup query in UpsertFeedbackAsync.
    private sealed class ExistingEntry
    {
        public string Id { get; init; } = string.Empty;
        public DateTimeOffset? CreatedAt { get; init; }
    }
}
