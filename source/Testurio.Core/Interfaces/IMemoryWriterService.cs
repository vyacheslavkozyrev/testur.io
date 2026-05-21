using Testurio.Core.Entities;
using Testurio.Core.Models;

namespace Testurio.Core.Interfaces;

/// <summary>
/// Contract for Stage 8 of the pipeline (feature 0046 / future feature 0032).
/// Embeds the parsed story and upserts effective test scenarios into the <c>TestMemory</c>
/// Cosmos DB container for use as few-shot examples in future generation calls.
/// Invoked only when all scenarios in the run passed AND <c>features.aiMemory == true</c>.
/// </summary>
public interface IMemoryWriterService
{
    /// <summary>
    /// Upserts the scenario to the <c>TestMemory</c> container.
    /// Called after a full-pass run when the user's plan enables <c>aiMemory</c>.
    /// </summary>
    /// <param name="story">The parsed story from Stage 1 — provides the text used for embedding.</param>
    /// <param name="generatorResults">The generator output from Stage 4 — scenarios to persist.</param>
    /// <param name="run">The completed <see cref="TestRun"/> — provides user/project context.</param>
    /// <param name="ct">Cancellation token forwarded to all I/O calls.</param>
    Task UpsertScenarioAsync(
        ParsedStory story,
        GeneratorResults generatorResults,
        TestRun run,
        CancellationToken ct = default);
}
