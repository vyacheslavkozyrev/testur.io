using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Testurio.Core.Entities;
using Testurio.Core.Repositories;
using Testurio.Infrastructure.Blob;
using Testurio.Plugins.TestExecutorPlugin;

namespace Testurio.UnitTests.Plugins;

public class LogPersistenceServiceTests
{
    private readonly Mock<IExecutionLogRepository> _repository = new();
    private readonly Mock<BlobStorageClient> _blobClient;

    public LogPersistenceServiceTests()
    {
        // BlobStorageClient has no parameterless constructor — mock it.
        _blobClient = new Mock<BlobStorageClient>(
            Mock.Of<Azure.Storage.Blobs.BlobServiceClient>(),
            "test-container",
            NullLogger<BlobStorageClient>.Instance);
    }

    private LogPersistenceService CreateSut() =>
        new(_repository.Object, _blobClient.Object, NullLogger<LogPersistenceService>.Instance);

    private static ExecutionLogEntry MakeEntry(string? responseBody = null) => new()
    {
        TestRunId = "run1",
        ProjectId = "proj1",
        UserId = "user1",
        ScenarioId = "s1",
        StepIndex = 0,
        StepTitle = "GET /api/items",
        HttpMethod = "GET",
        RequestUrl = "https://app.example.com/api/items",
        DurationMs = 120,
        ResponseBodyInline = responseBody
    };

    // — Inline path (AC-005) —

    [Fact]
    public async Task PersistAsync_SmallBody_StoresInlineWithoutBlobUpload()
    {
        // Body is well under 10 KB.
        var entry = MakeEntry("{\"id\":1}");

        _repository.Setup(r => r.PersistAsync(It.IsAny<ExecutionLogEntry>(), default))
            .Returns(Task.CompletedTask);

        var sut = CreateSut();
        await sut.PersistAsync(entry);

        // Blob upload must NOT have been attempted.
        _blobClient.Verify(b => b.UploadAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);

        // Body is still inline; no blob URL.
        Assert.Equal("{\"id\":1}", entry.ResponseBodyInline);
        Assert.Null(entry.ResponseBodyBlobUrl);
        Assert.False(entry.ResponseTruncated);
        _repository.Verify(r => r.PersistAsync(entry, default), Times.Once);
    }

    // — Blob path (AC-006) —

    [Fact]
    public async Task PersistAsync_LargeBody_UploadsToBlobAndClearsInline()
    {
        // Body larger than 10 KB.
        var largeBody = new string('x', BlobStorageClient.InlineThresholdBytes + 1);
        var entry = MakeEntry(largeBody);

        _blobClient
            .Setup(b => b.UploadAsync(It.IsAny<string>(), largeBody, It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://blob.example.com/logs/run1/s1/0.txt");

        _repository.Setup(r => r.PersistAsync(It.IsAny<ExecutionLogEntry>(), default))
            .Returns(Task.CompletedTask);

        var sut = CreateSut();
        await sut.PersistAsync(entry);

        // Inline body cleared; blob URL set.
        Assert.Null(entry.ResponseBodyInline);
        Assert.Equal("https://blob.example.com/logs/run1/s1/0.txt", entry.ResponseBodyBlobUrl);
        Assert.False(entry.ResponseTruncated);
        _repository.Verify(r => r.PersistAsync(entry, default), Times.Once);
    }

    // — Blob upload failure with truncation (AC-008) —

    [Fact]
    public async Task PersistAsync_BlobUploadFails_TruncatesBodyAndFlagsEntry()
    {
        var largeBody = new string('x', BlobStorageClient.InlineThresholdBytes + 1);
        var entry = MakeEntry(largeBody);

        // Blob upload returns null — simulating failure.
        _blobClient
            .Setup(b => b.UploadAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        _repository.Setup(r => r.PersistAsync(It.IsAny<ExecutionLogEntry>(), default))
            .Returns(Task.CompletedTask);

        var sut = CreateSut();
        await sut.PersistAsync(entry);

        // Body must be truncated to threshold; truncation flag must be set.
        Assert.NotNull(entry.ResponseBodyInline);
        Assert.True(
            System.Text.Encoding.UTF8.GetByteCount(entry.ResponseBodyInline!) <= BlobStorageClient.InlineThresholdBytes,
            "Inline body exceeds threshold after truncation");
        Assert.True(entry.ResponseTruncated);
        Assert.Null(entry.ResponseBodyBlobUrl);
        _repository.Verify(r => r.PersistAsync(entry, default), Times.Once);
    }

    // — Persistence failure is non-fatal (AC-004) —

    [Fact]
    public async Task PersistAsync_RepositoryThrows_DoesNotPropagateException()
    {
        var entry = MakeEntry("small body");

        _repository.Setup(r => r.PersistAsync(It.IsAny<ExecutionLogEntry>(), default))
            .ThrowsAsync(new InvalidOperationException("Cosmos unavailable"));

        var sut = CreateSut();

        // Must not throw — failure is absorbed with a warning log.
        var ex = await Record.ExceptionAsync(() => sut.PersistAsync(entry));
        Assert.Null(ex);
    }

    [Fact]
    public async Task PersistAsync_NullResponseBody_SkipsBlobRoutingAndPersists()
    {
        var entry = MakeEntry(null);

        _repository.Setup(r => r.PersistAsync(It.IsAny<ExecutionLogEntry>(), default))
            .Returns(Task.CompletedTask);

        var sut = CreateSut();
        await sut.PersistAsync(entry);

        _blobClient.Verify(b => b.UploadAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);

        Assert.Null(entry.ResponseBodyInline);
        Assert.Null(entry.ResponseBodyBlobUrl);
        Assert.False(entry.ResponseTruncated);
        _repository.Verify(r => r.PersistAsync(entry, default), Times.Once);
    }
}
