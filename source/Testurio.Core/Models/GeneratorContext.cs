using Testurio.Core.Entities;

namespace Testurio.Core.Models;

/// <summary>
/// All inputs required by a generator agent to produce test scenarios.
/// Constructed by <c>TestRunJobProcessor</c> (stage 4) and passed to each
/// <c>ITestGeneratorAgent</c> implementation before calling <c>GenerateAsync</c>.
/// </summary>
public sealed record GeneratorContext
{
    /// <summary>
    /// The structured story output of the StoryParser stage (stage 1).
    /// Contains title, description, acceptance criteria, entities, actions, and edge cases.
    /// </summary>
    public required ParsedStory ParsedStory { get; init; }

    /// <summary>
    /// Top-k semantically similar past scenarios retrieved by the MemoryRetrieval stage (stage 3).
    /// <see cref="MemoryRetrievalResult.Scenarios"/> may be empty on first run or when no similar scenarios exist.
    /// </summary>
    public required MemoryRetrievalResult MemoryRetrievalResult { get; init; }

    /// <summary>
    /// The project configuration. Provides <c>TestingStrategy</c> and optional <c>CustomPrompt</c>
    /// used as context layers in the assembled Claude prompt.
    /// </summary>
    public required Project ProjectConfig { get; init; }

    /// <summary>
    /// The prompt template for this agent type, loaded from the <c>PromptTemplates</c> Cosmos container.
    /// Provides the system prompt, generator instruction, and max scenario count.
    /// </summary>
    public required PromptTemplate PromptTemplate { get; init; }

    /// <summary>
    /// Unique identifier of the current pipeline run.
    /// Included in structured log entries for correlation.
    /// </summary>
    public required Guid TestRunId { get; init; }
}
