using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Logging;
using Testurio.Core.Interfaces;

namespace Testurio.Infrastructure.Storage;

/// <summary>
/// Azure Blob Storage implementation of <see cref="IScreenshotStorage"/>.
/// Uploads Playwright screenshot PNG bytes to the <c>test-screenshots</c> container
/// at path <c>{userId}/{runId}/{scenarioId}/step-{stepIndex}.png</c> and returns
/// the full blob URI on success.
/// </summary>
public sealed partial class BlobScreenshotStorage : IScreenshotStorage
{
    private const string ContainerName = "test-screenshots";

    private readonly BlobServiceClient _serviceClient;
    private readonly ILogger<BlobScreenshotStorage> _logger;

    // Guards the one-time container creation check. A SemaphoreSlim ensures only one
    // concurrent caller performs CreateIfNotExistsAsync; all subsequent callers skip it.
    private readonly SemaphoreSlim _containerInitLock = new(1, 1);
    private volatile bool _containerEnsured;

    public BlobScreenshotStorage(
        BlobServiceClient serviceClient,
        ILogger<BlobScreenshotStorage> logger)
    {
        _serviceClient = serviceClient;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<string> UploadAsync(
        string userId,
        Guid runId,
        string scenarioId,
        int stepIndex,
        byte[] png,
        CancellationToken ct = default)
    {
        var blobName = $"{userId}/{runId}/{scenarioId}/step-{stepIndex}.png";
        var containerClient = _serviceClient.GetBlobContainerClient(ContainerName);

        if (!_containerEnsured)
        {
            await _containerInitLock.WaitAsync(ct);
            try
            {
                if (!_containerEnsured)
                {
                    await containerClient.CreateIfNotExistsAsync(
                        publicAccessType: PublicAccessType.None,
                        cancellationToken: ct);
                    _containerEnsured = true;
                }
            }
            finally
            {
                _containerInitLock.Release();
            }
        }

        var blobClient = containerClient.GetBlobClient(blobName);

        using var stream = new MemoryStream(png);
        await blobClient.UploadAsync(stream, new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = "image/png" }
        }, ct);

        LogUploaded(_logger, blobName);
        return blobClient.Uri.ToString();
    }

    [LoggerMessage(Level = LogLevel.Debug,
        Message = "Screenshot blob uploaded: {BlobName}")]
    private static partial void LogUploaded(ILogger logger, string blobName);
}
