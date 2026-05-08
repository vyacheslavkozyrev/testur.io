using Moq;
using Testurio.Core.Entities;
using Testurio.Core.Repositories;

namespace Testurio.UnitTests.Infrastructure;

/// <summary>
/// Unit tests for <see cref="IExecutionLogRepository"/> behavioral contract.
/// These tests exercise the contract through a mock implementation, verifying
/// that callers interact with the repository correctly. Cosmos DB integration is
/// covered by the integration test suite.
/// </summary>
public class ExecutionLogRepositoryTests
{
    private readonly Mock<IExecutionLogRepository> _repository = new();

    private static ExecutionLogEntry MakeEntry(
        string testRunId = "run1",
        string scenarioId = "s1",
        int stepIndex = 0,
        string? responseBodyInline = "{\"id\":1}",
        string? responseBlobUrl = null) => new()
    {
        TestRunId = testRunId,
        ProjectId = "proj1",
        UserId = "user1",
        ScenarioId = scenarioId,
        StepIndex = stepIndex,
        StepTitle = "GET /api/items",
        HttpMethod = "GET",
        RequestUrl = "https://app.example.com/api/items",
        DurationMs = 100,
        ResponseBodyInline = responseBodyInline,
        ResponseBodyBlobUrl = responseBlobUrl
    };

    // — Persist —

    [Fact]
    public async Task PersistAsync_CallsRepositoryWithEntry()
    {
        var entry = MakeEntry();
        _repository.Setup(r => r.PersistAsync(entry, default)).Returns(Task.CompletedTask);

        await _repository.Object.PersistAsync(entry);

        _repository.Verify(r => r.PersistAsync(entry, default), Times.Once);
    }

    // — Get by run ID —

    [Fact]
    public async Task GetByRunAsync_ReturnsAllEntriesForRun()
    {
        var e1 = MakeEntry(stepIndex: 0);
        var e2 = MakeEntry(stepIndex: 1);

        _repository.Setup(r => r.GetByRunAsync("proj1", "run1", default))
            .ReturnsAsync(new[] { e1, e2 });

        var results = await _repository.Object.GetByRunAsync("proj1", "run1");

        Assert.Equal(2, results.Count);
        Assert.Contains(results, r => r.StepIndex == 0);
        Assert.Contains(results, r => r.StepIndex == 1);
    }

    [Fact]
    public async Task GetByRunAsync_DifferentRun_ReturnsEmpty()
    {
        _repository.Setup(r => r.GetByRunAsync("proj1", "run-other", default))
            .ReturnsAsync(Array.Empty<ExecutionLogEntry>());

        var results = await _repository.Object.GetByRunAsync("proj1", "run-other");

        Assert.Empty(results);
    }

    // — Get by step ID —

    [Fact]
    public async Task GetByStepAsync_ReturnsMatchingEntry()
    {
        var entry = MakeEntry(scenarioId: "s1", stepIndex: 2);

        _repository.Setup(r => r.GetByStepAsync("proj1", "run1", "s1", 2, default))
            .ReturnsAsync(entry);

        var result = await _repository.Object.GetByStepAsync("proj1", "run1", "s1", 2);

        Assert.NotNull(result);
        Assert.Equal(2, result!.StepIndex);
        Assert.Equal("s1", result.ScenarioId);
    }

    [Fact]
    public async Task GetByStepAsync_NotFound_ReturnsNull()
    {
        _repository.Setup(r => r.GetByStepAsync("proj1", "run1", "s1", 99, default))
            .ReturnsAsync((ExecutionLogEntry?)null);

        var result = await _repository.Object.GetByStepAsync("proj1", "run1", "s1", 99);

        Assert.Null(result);
    }

    // — Blob URL resolution (AC-007) —

    [Fact]
    public async Task GetByRunAsync_BlobStoredEntry_ReturnsBlobUrlNotInlineBody()
    {
        // Entry was stored with blob URL (large response body).
        var entry = MakeEntry(
            responseBodyInline: null,
            responseBlobUrl: "https://blob.example.com/logs/run1/s1/0.txt");

        _repository.Setup(r => r.GetByRunAsync("proj1", "run1", default))
            .ReturnsAsync(new[] { entry });

        var results = await _repository.Object.GetByRunAsync("proj1", "run1");

        Assert.Single(results);
        Assert.Null(results[0].ResponseBodyInline);
        Assert.Equal("https://blob.example.com/logs/run1/s1/0.txt", results[0].ResponseBodyBlobUrl);
    }

    // — Delete by run ID (AC-010) —

    [Fact]
    public async Task DeleteByRunAsync_CallsRepositoryWithCorrectRunId()
    {
        _repository.Setup(r => r.DeleteByRunAsync("proj1", "run1", default))
            .Returns(Task.CompletedTask);

        await _repository.Object.DeleteByRunAsync("proj1", "run1");

        _repository.Verify(r => r.DeleteByRunAsync("proj1", "run1", default), Times.Once);
    }
}
