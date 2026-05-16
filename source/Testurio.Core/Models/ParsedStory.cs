namespace Testurio.Core.Models;

/// <summary>
/// Structured output of the StoryParser stage (stage 1), shared with all downstream pipeline stages.
/// Immutable — produced once by the StoryParser and consumed read-only by AgentRouter, MemoryRetrieval,
/// and Generators.
/// </summary>
public sealed record ParsedStory
{
    /// <summary>Story title. Never null or empty.</summary>
    public required string Title { get; init; }

    /// <summary>Story description body. Never null or empty.</summary>
    public required string Description { get; init; }

    /// <summary>One or more acceptance criteria. Contains at least one entry.</summary>
    public required IReadOnlyList<string> AcceptanceCriteria { get; init; }

    /// <summary>Domain entities detected from description and AC text. Empty array when none detected.</summary>
    public IReadOnlyList<string> Entities { get; init; } = Array.Empty<string>();

    /// <summary>User or system actions detected from description and AC text. Empty array when none detected.</summary>
    public IReadOnlyList<string> Actions { get; init; } = Array.Empty<string>();

    /// <summary>Edge cases detected from description and AC text. Empty array when none detected.</summary>
    public IReadOnlyList<string> EdgeCases { get; init; } = Array.Empty<string>();
}
