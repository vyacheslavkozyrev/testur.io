using Microsoft.Extensions.Logging;
using Testurio.Core.Entities;
using Testurio.Core.Interfaces;
using Testurio.Core.Models;

namespace Testurio.Pipeline.MemoryRetrieval;

/// <summary>
/// Implements the MemoryRetrieval pipeline stage (stage 3).
/// Embeds the parsed story text and retrieves the top-3 semantically similar past test
/// scenarios from the <c>TestMemory</c> Cosmos DB container.
/// Any infrastructure failure is caught, a structured warning is emitted, and an empty
/// <see cref="MemoryRetrievalResult"/> is returned so the pipeline continues to stage 4.
/// </summary>
public sealed partial class MemoryRetrievalService : IMemoryRetrievalService
{
    private readonly IEmbeddingService _embeddingService;
    private readonly ITestMemoryRepository _memoryRepository;
    private readonly ILogger<MemoryRetrievalService> _logger;

    public MemoryRetrievalService(
        IEmbeddingService embeddingService,
        ITestMemoryRepository memoryRepository,
        ILogger<MemoryRetrievalService> logger)
    {
        _embeddingService = embeddingService;
        _memoryRepository = memoryRepository;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<MemoryRetrievalResult> RetrieveAsync(
        ParsedStory story,
        Project project,
        string runId,
        CancellationToken cancellationToken = default)
    {
        // Build a single text string from the parsed story for embedding.
        var storyText = BuildStoryText(story);

        float[] embedding;
        try
        {
            embedding = await _embeddingService.EmbedAsync(storyText, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            LogEmbeddingFailed(_logger, runId, project.UserId, project.Id, ex);
            return EmptyResult();
        }

        IReadOnlyList<TestMemoryEntry> entries;
        try
        {
            entries = await _memoryRepository.FindSimilarAsync(
                project.UserId,
                project.Id,
                embedding,
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            LogQueryFailed(_logger, runId, project.UserId, project.Id, ex);
            return EmptyResult();
        }

        LogRetrieved(_logger, runId, project.UserId, project.Id, entries.Count);

        return new MemoryRetrievalResult { Scenarios = entries };
    }

    private static MemoryRetrievalResult EmptyResult() =>
        new() { Scenarios = Array.Empty<TestMemoryEntry>() };

    /// <summary>
    /// Concatenates the story's title, description, acceptance criteria, entities, actions,
    /// and edge cases into a single string for embedding. This mirrors the information
    /// used by the generator agents so that the similarity search is semantically aligned.
    /// </summary>
    private static string BuildStoryText(ParsedStory story)
    {
        var parts = new List<string>
        {
            story.Title,
            story.Description
        };

        if (story.AcceptanceCriteria.Count > 0)
            parts.Add(string.Join("\n", story.AcceptanceCriteria));

        if (story.Entities.Count > 0)
            parts.Add(string.Join(", ", story.Entities));

        if (story.Actions.Count > 0)
            parts.Add(string.Join(", ", story.Actions));

        if (story.EdgeCases.Count > 0)
            parts.Add(string.Join(", ", story.EdgeCases));

        return string.Join("\n\n", parts);
    }

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "MemoryRetrieval: embedding failed for run {RunId} (userId={UserId}, projectId={ProjectId}) — returning empty result")]
    private static partial void LogEmbeddingFailed(ILogger logger, string runId, string userId, string projectId, Exception ex);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "MemoryRetrieval: Cosmos vector query failed for run {RunId} (userId={UserId}, projectId={ProjectId}) — returning empty result")]
    private static partial void LogQueryFailed(ILogger logger, string runId, string userId, string projectId, Exception ex);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "MemoryRetrieval: retrieved {Count} scenario(s) for run {RunId} (userId={UserId}, projectId={ProjectId})")]
    private static partial void LogRetrieved(ILogger logger, string runId, string userId, string projectId, int count);
}
