using Testurio.Core.Entities;
using Testurio.Core.Models;

namespace Testurio.Core.Interfaces;

/// <summary>
/// Contract for the MemoryRetrieval pipeline stage (stage 3).
/// Embeds the parsed story text and retrieves semantically similar past test scenarios
/// from the <c>TestMemory</c> Cosmos DB container to use as few-shot context in stage 4.
/// </summary>
public interface IMemoryRetrievalService
{
    /// <summary>
    /// Embeds <paramref name="story"/> and returns the top-3 most semantically similar
    /// past scenarios scoped to the project identified by <paramref name="project"/>.
    /// </summary>
    /// <param name="story">Structured story output from stage 1 (StoryParser).</param>
    /// <param name="project">Project configuration, providing <c>UserId</c> and <c>Id</c> for scoping.</param>
    /// <param name="runId">Current test run identifier, included in warning logs on failure.</param>
    /// <param name="cancellationToken">Cancellation token forwarded to all async I/O operations.</param>
    /// <returns>
    /// A <see cref="MemoryRetrievalResult"/> whose <see cref="MemoryRetrievalResult.Scenarios"/> list
    /// contains 0–3 entries. Never throws — any infrastructure failure is caught, logged, and
    /// returns an empty result so that the pipeline continues to stage 4.
    /// </returns>
    Task<MemoryRetrievalResult> RetrieveAsync(
        ParsedStory story,
        Project project,
        string runId,
        CancellationToken cancellationToken = default);
}
