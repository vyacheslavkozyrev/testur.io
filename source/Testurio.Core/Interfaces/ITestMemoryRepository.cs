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

    /// <summary>
    /// Upserts a QA-lead feedback entry to the <c>TestMemory</c> container (feature 0031).
    /// The upsert key is <paramref name="workItemId"/> + <paramref name="testType"/> + <c>source="qalead"</c>
    /// within the <paramref name="userId"/> partition.
    /// If an existing document matches, it is overwritten in full while preserving <c>id</c> and <c>createdAt</c>.
    /// If no document matches, a new document is inserted with a generated UUID v4 <c>id</c> and
    /// <c>createdAt = UtcNow</c>. <c>updatedAt</c> is always set to <c>UtcNow</c>.
    /// <c>passRate</c> and <c>runCount</c> are omitted — feedback entries are not subject to the
    /// quality loop soft-delete.
    /// </summary>
    /// <param name="userId">Azure AD B2C OID — partition key.</param>
    /// <param name="projectId">Project UUID — scopes this entry to the originating project.</param>
    /// <param name="testType">Test type active on the last run for this work item (<c>api</c> or <c>ui_e2e</c>).</param>
    /// <param name="feedbackText">Trimmed comment body after stripping the <c>@testurio memorize</c> flag.</param>
    /// <param name="storyEmbedding">1536-dimensional embedding of <paramref name="feedbackText"/>.</param>
    /// <param name="workItemId">Work item / issue identifier — forms part of the upsert key.</param>
    /// <param name="commentId">PM-tool-assigned comment identifier stored on the document.</param>
    /// <param name="cancellationToken">Cancellation token forwarded to Cosmos SDK calls.</param>
    Task UpsertFeedbackAsync(
        string userId,
        Guid projectId,
        string testType,
        string feedbackText,
        float[] storyEmbedding,
        string workItemId,
        string commentId,
        CancellationToken cancellationToken = default);
}
