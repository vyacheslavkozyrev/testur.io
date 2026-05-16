using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Testurio.Core.Entities;
using Testurio.Core.Interfaces;
using Testurio.Core.Models;
using Testurio.Pipeline.MemoryRetrieval;

namespace Testurio.IntegrationTests.Pipeline;

/// <summary>
/// Integration tests for the MemoryRetrieval pipeline stage (feature 0027).
/// Exercises the full retrieval path through <see cref="MemoryRetrievalService"/> with a mocked
/// <see cref="IEmbeddingService"/> and mocked <see cref="ITestMemoryRepository"/>.
/// Validates end-to-end behaviour from <see cref="ParsedStory"/> input to
/// <see cref="MemoryRetrievalResult"/> output, covering cold start and pre-seeded scenarios.
/// Note: a Cosmos-emulator-backed test covering the real DiskANN query is tracked for manual
/// implementation (see progress.md — remaining issues).
/// </summary>
public class MemoryRetrievalIntegrationTests
{
    private readonly Mock<IEmbeddingService> _embeddingService = new();
    private readonly Mock<ITestMemoryRepository> _repository = new();

    private static readonly float[] SampleEmbedding = new float[1536];

    private MemoryRetrievalService CreateService() =>
        new(_embeddingService.Object, _repository.Object, NullLogger<MemoryRetrievalService>.Instance);

    private static ParsedStory MakeApiStory() => new()
    {
        Title = "Create order endpoint",
        Description = "The API exposes a POST /orders endpoint that persists an order.",
        AcceptanceCriteria = new[]
        {
            "POST /orders returns HTTP 201 Created",
            "Response body contains the created order ID",
            "Order is persisted in the database"
        },
        Entities = new[] { "Order", "Customer" },
        Actions = new[] { "POST", "persist" },
        EdgeCases = new[] { "Missing required fields returns 400", "Duplicate order returns 409" }
    };

    private static ParsedStory MakeUiStory() => new()
    {
        Title = "Checkout flow",
        Description = "User completes checkout from the shopping cart page.",
        AcceptanceCriteria = new[] { "Confirmation page shows order number" }
    };

    private static Project MakeProject(string userId = "user-int-1", string projectId = "proj-int-1") => new()
    {
        Id = projectId,
        UserId = userId,
        Name = "Integration Test Project",
        ProductUrl = "https://app.example.com",
        TestingStrategy = "api"
    };

    private static TestMemoryEntry MakeEntry(int n, string userId = "user-int-1", string projectId = "proj-int-1") => new()
    {
        Id = $"memory-{n}",
        UserId = userId,
        ProjectId = projectId,
        TestType = "api",
        StoryText = $"Past story text {n}",
        ScenarioText = $"{{\"steps\": [{{\"action\": \"POST /orders\", \"expected\": 201}}]}}",
        PassRate = 0.95,
        RunCount = 5,
        IsDeleted = false
    };

    // ─── pre-seeded entries → top-3 forwarded to the result ──────────────────

    [Fact]
    public async Task FullRetrieval_PreSeededEntries_TopThreeForwardedToResult()
    {
        var seededEntries = new[] { MakeEntry(1), MakeEntry(2), MakeEntry(3) };

        _embeddingService.Setup(e => e.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SampleEmbedding);
        _repository.Setup(r => r.FindSimilarAsync(
                "user-int-1", "proj-int-1", SampleEmbedding, It.IsAny<CancellationToken>()))
            .ReturnsAsync(seededEntries);

        var sut = CreateService();
        var result = await sut.RetrieveAsync(MakeApiStory(), MakeProject(), "run-int-1");

        // All 3 pre-seeded entries are present in the result.
        Assert.Equal(3, result.Scenarios.Count);
        Assert.Equal("memory-1", result.Scenarios[0].Id);
        Assert.Equal("memory-2", result.Scenarios[1].Id);
        Assert.Equal("memory-3", result.Scenarios[2].Id);

        // Each entry exposes the required fields (AC-007).
        var first = result.Scenarios[0];
        Assert.NotEmpty(first.StoryText);
        Assert.NotEmpty(first.ScenarioText);
        Assert.NotEmpty(first.TestType);
        Assert.True(first.PassRate >= 0.0 && first.PassRate <= 1.0);
        Assert.True(first.RunCount >= 0);

        // Embedding was called once with a non-empty string (the composed story text).
        _embeddingService.Verify(e => e.EmbedAsync(
            It.Is<string>(s => s.Contains("Create order endpoint")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ─── no entries → empty result, pipeline continues to stage 4 ─────────────

    [Fact]
    public async Task FullRetrieval_NoEntries_EmptyResultAndPipelineContinues()
    {
        _embeddingService.Setup(e => e.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SampleEmbedding);
        _repository.Setup(r => r.FindSimilarAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<float[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<TestMemoryEntry>());

        var sut = CreateService();

        // Must not throw — the pipeline continues with an empty memory result.
        var result = await sut.RetrieveAsync(MakeApiStory(), MakeProject(), "run-int-2");

        // AC-009: empty list is the correct cold-start response.
        Assert.NotNull(result);
        Assert.Empty(result.Scenarios);

        // Stage 4 (generators) should receive the result regardless — the service returns normally.
        // This verifies AC-011 indirectly: the call did not throw, so the caller can pass the
        // empty result to the generators without additional guard logic.
    }

    // ─── retrieval is scoped to userId + projectId (AC-003) ──────────────────

    [Fact]
    public async Task FullRetrieval_RepositoryCalledWithCorrectProjectScope()
    {
        var project = MakeProject(userId: "user-scope", projectId: "proj-scope");

        _embeddingService.Setup(e => e.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SampleEmbedding);
        _repository.Setup(r => r.FindSimilarAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<float[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<TestMemoryEntry>());

        var sut = CreateService();
        await sut.RetrieveAsync(MakeUiStory(), project, "run-int-3");

        // The repository must be called with the exact userId and projectId — no cross-project leak.
        _repository.Verify(r => r.FindSimilarAsync(
            "user-scope", "proj-scope", It.IsAny<float[]>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ─── mocked IEmbeddingService and repository used (no live Azure calls) ──

    [Fact]
    public async Task FullRetrieval_EmbeddingServiceUsed_VectorPassedToRepository()
    {
        var specificEmbedding = Enumerable.Range(0, 1536).Select(i => (float)i / 1536f).ToArray();

        _embeddingService.Setup(e => e.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(specificEmbedding);
        _repository.Setup(r => r.FindSimilarAsync(
                It.IsAny<string>(), It.IsAny<string>(), specificEmbedding, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { MakeEntry(1) });

        var sut = CreateService();
        var result = await sut.RetrieveAsync(MakeApiStory(), MakeProject(), "run-int-4");

        // The embedding vector returned by IEmbeddingService is passed to the repository.
        Assert.Single(result.Scenarios);
        _repository.Verify(r => r.FindSimilarAsync(
            "user-int-1", "proj-int-1", specificEmbedding, It.IsAny<CancellationToken>()), Times.Once);
    }
}
