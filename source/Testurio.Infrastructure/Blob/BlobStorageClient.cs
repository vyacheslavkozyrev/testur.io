using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Logging;

namespace Testurio.Infrastructure.Blob;

/// <summary>
/// Uploads response body content to Azure Blob Storage for log entries that exceed the
/// inline size threshold (10 KB).  Returns the public URL on success.
/// On failure, indicates truncation is needed so the caller can inline up to 10 KB and flag the entry.
/// </summary>
public partial class BlobStorageClient
{
    /// <summary>Response bodies up to this size are stored inline in Cosmos DB.</summary>
    public const int InlineThresholdBytes = 10 * 1024; // 10 KB

    private readonly BlobServiceClient _serviceClient;
    private readonly string _containerName;
    private readonly ILogger<BlobStorageClient> _logger;

    public BlobStorageClient(
        BlobServiceClient serviceClient,
        string containerName,
        ILogger<BlobStorageClient> logger)
    {
        _serviceClient = serviceClient;
        _containerName = containerName;
        _logger = logger;
    }

    /// <summary>
    /// Uploads <paramref name="content"/> to blob storage and returns the blob URL.
    /// Returns null and logs a warning when the upload fails (AC-008).
    /// </summary>
    /// <param name="blobName">Unique name for the blob (e.g. "logs/{runId}/{stepIndex}.txt").</param>
    /// <param name="content">Raw text content to upload.</param>
    /// <param name="cancellationToken">Propagated to the Azure SDK.</param>
    /// <returns>Absolute URL of the uploaded blob, or null if the upload failed.</returns>
    public virtual async Task<string?> UploadAsync(
        string blobName,
        string content,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var containerClient = _serviceClient.GetBlobContainerClient(_containerName);
            await containerClient.CreateIfNotExistsAsync(
                publicAccessType: PublicAccessType.None,
                cancellationToken: cancellationToken);

            var blobClient = containerClient.GetBlobClient(blobName);
            using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content));
            await blobClient.UploadAsync(stream, overwrite: true, cancellationToken: cancellationToken);

            LogUploaded(_logger, blobName);
            return blobClient.Uri.ToString();
        }
        catch (Exception ex)
        {
            LogUploadFailed(_logger, blobName, ex);
            return null;
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Response body blob uploaded: {BlobName}")]
    private static partial void LogUploaded(ILogger logger, string blobName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Blob upload failed for '{BlobName}' — response body will be truncated")]
    private static partial void LogUploadFailed(ILogger logger, string blobName, Exception ex);
}
