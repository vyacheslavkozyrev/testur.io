using Microsoft.Extensions.Logging;
using Testurio.Core.Entities;
using Testurio.Core.Interfaces;
using Testurio.Core.Models;

namespace Testurio.Pipeline.MemoryWriter;

/// <summary>
/// Stage 8 of the pipeline (feature 0046 / full implementation in feature 0032).
/// Embeds the parsed story and upserts effective test scenarios into the <c>TestMemory</c>
/// Cosmos DB container when all scenarios in the run passed.
/// <para>
/// This implementation is a functional stub: it logs the upsert intent but does not yet call
/// <see cref="IEmbeddingService"/> or <see cref="ITestMemoryRepository"/>. The full embedding +
/// vector upsert logic ships with feature 0032. The stub ensures the pipeline wiring and the
/// <c>aiMemory</c> flag gate are functional before that feature is implemented.
/// </para>
/// </summary>
public sealed partial class MemoryWriterService : IMemoryWriterService
{
    private readonly ILogger<MemoryWriterService> _logger;

    public MemoryWriterService(ILogger<MemoryWriterService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task UpsertScenarioAsync(
        ParsedStory story,
        GeneratorResults generatorResults,
        TestRun run,
        CancellationToken ct = default)
    {
        // Feature 0032 will replace this stub with actual embedding + Cosmos upsert.
        // For now, log that the stage was invoked so the pipeline wiring is observable.
        LogUpsertStub(
            _logger,
            run.Id,
            generatorResults.ApiScenarios.Count + generatorResults.UiE2eScenarios.Count);

        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Debug,
        Message = "MemoryWriter: stage invoked for run {RunId} with {ScenarioCount} scenario(s) — full implementation deferred to feature 0032")]
    private static partial void LogUpsertStub(ILogger logger, string runId, int scenarioCount);
}
