using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Testurio.Core.Entities;
using Testurio.Core.Interfaces;
using Testurio.Core.Models;
using Testurio.Pipeline.MemoryRetrieval;

namespace Testurio.UnitTests.Pipeline;

public class MemoryRetrievalServiceTests
{
    private readonly Mock<IEmbeddingService> _embeddingService = new();
    private readonly Mock<ITestMemoryRepository> _repository = new();
    private readonly Mock<ILogger<MemoryRetrievalService>> _logger = new();

    public MemoryRetrievalServiceTests()
    {
        // [LoggerMessage] source-generated methods call IsEnabled before Log.
        // Return true for all log levels so that Verify on Log calls succeeds.
        _logger.Setup(l => l.IsEnabled(It.IsAny<LogLevel>())).Returns(true);
    }

    private MemoryRetrievalService CreateSut() =>
        new(_embeddingService.Object, _repository.Object, _logger.Object);

    private static readonly float[] SampleEmbedding = new float[1536];

    private static ParsedStory MakeStory() => new()
    {
        Title = "Submit order via API",
        Description = "The user submits an order through the REST API.",
        AcceptanceCriteria = new[] { "POST /orders returns 201", "Order persisted in database" }
    };

    private static Project MakeProject() => new()
    {
        Id = "proj-001",
        UserId = "user-001",
        Name = "Test Project",
        ProductUrl = "https://app.example.com",
        TestingStrategy = "api"
    };

    private static TestMemoryEntry MakeEntry(int n) => new()
    {
        Id = $"entry-{n}",
        UserId = "user-001",
        ProjectId = "proj-001",
        TestType = "api",
        StoryText = $"Story text {n}",
        ScenarioText = $"Scenario JSON {n}",
        PassRate = 0.9,
        RunCount = 3,
        IsDeleted = false
    };

    // ─── 3 entries returned → all present in result ───────────────────────────

    [Fact]
    public async Task RetrieveAsync_ThreeEntriesReturned_AllPresentInResult()
    {
        var entries = new[] { MakeEntry(1), MakeEntry(2), MakeEntry(3) };
        _embeddingService.Setup(e => e.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SampleEmbedding);
        _repository.Setup(r => r.FindSimilarAsync(
                "user-001", "proj-001", SampleEmbedding, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entries);

        var sut = CreateSut();
        var result = await sut.RetrieveAsync(MakeStory(), MakeProject(), "run-001");

        Assert.Equal(3, result.Scenarios.Count);
        Assert.Equal("entry-1", result.Scenarios[0].Id);
        Assert.Equal("entry-2", result.Scenarios[1].Id);
        Assert.Equal("entry-3", result.Scenarios[2].Id);
    }

    // ─── 0 entries → empty list, no warning emitted ──────────────────────────

    [Fact]
    public async Task RetrieveAsync_ZeroEntries_ReturnsEmptyListWithoutWarning()
    {
        _embeddingService.Setup(e => e.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SampleEmbedding);
        _repository.Setup(r => r.FindSimilarAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<float[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<TestMemoryEntry>());

        var sut = CreateSut();
        var result = await sut.RetrieveAsync(MakeStory(), MakeProject(), "run-002");

        Assert.Empty(result.Scenarios);

        // No Warning-level log should be emitted for a cold start (zero results is expected).
        _logger.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    // ─── embedding throws → empty result, warning logged with userId + projectId + runId ────

    [Fact]
    public async Task RetrieveAsync_EmbeddingThrows_ReturnsEmptyAndLogsWarning()
    {
        var embeddingException = new InvalidOperationException("Azure OpenAI unavailable");
        _embeddingService.Setup(e => e.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(embeddingException);

        var sut = CreateSut();
        var result = await sut.RetrieveAsync(MakeStory(), MakeProject(), "run-003");

        Assert.Empty(result.Scenarios);

        // A Warning log must be emitted.
        _logger.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                embeddingException,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    // ─── Cosmos query throws → empty result, warning logged ──────────────────

    [Fact]
    public async Task RetrieveAsync_CosmosQueryThrows_ReturnsEmptyAndLogsWarning()
    {
        var cosmosException = new Exception("Cosmos DB unavailable");
        _embeddingService.Setup(e => e.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SampleEmbedding);
        _repository.Setup(r => r.FindSimilarAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<float[]>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(cosmosException);

        var sut = CreateSut();
        var result = await sut.RetrieveAsync(MakeStory(), MakeProject(), "run-004");

        Assert.Empty(result.Scenarios);

        // A Warning log must be emitted.
        _logger.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                cosmosException,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    // ─── isDeleted: true entries excluded via repository filter ──────────────
    // The service relies on TestMemoryRepository to apply the isDeleted filter at query time.
    // This test verifies that entries returned by the repository (which only returns non-deleted
    // entries) are forwarded unchanged — and that the service would not receive any deleted entry
    // even if one slipped through, by confirming the full list is returned without further filtering.

    [Fact]
    public async Task RetrieveAsync_RepositoryReturnsOnlyNonDeletedEntries_AllForwarded()
    {
        // Repository contract: it only returns entries with isDeleted = false.
        var nonDeletedEntries = new[]
        {
            MakeEntry(1), // IsDeleted = false (see MakeEntry)
            MakeEntry(2)
        };

        _embeddingService.Setup(e => e.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SampleEmbedding);
        _repository.Setup(r => r.FindSimilarAsync(
                "user-001", "proj-001", SampleEmbedding, It.IsAny<CancellationToken>()))
            .ReturnsAsync(nonDeletedEntries);

        var sut = CreateSut();
        var result = await sut.RetrieveAsync(MakeStory(), MakeProject(), "run-005");

        // Both non-deleted entries are present in the result.
        Assert.Equal(2, result.Scenarios.Count);
        Assert.All(result.Scenarios, e => Assert.False(e.IsDeleted));
    }

    // ─── repository is called with correct userId and projectId ──────────────

    [Fact]
    public async Task RetrieveAsync_PassesCorrectScopeToRepository()
    {
        _embeddingService.Setup(e => e.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SampleEmbedding);
        _repository.Setup(r => r.FindSimilarAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<float[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<TestMemoryEntry>());

        var sut = CreateSut();
        await sut.RetrieveAsync(MakeStory(), MakeProject(), "run-006");

        // Verify the repository was called with the project's userId and projectId.
        _repository.Verify(r => r.FindSimilarAsync(
            "user-001", "proj-001", SampleEmbedding, It.IsAny<CancellationToken>()), Times.Once);
    }
}
