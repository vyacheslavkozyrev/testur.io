using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Logging;
using Testurio.Core.Interfaces;

namespace Testurio.Infrastructure.Blob;

/// <summary>
/// Azure Blob Storage client.  Provides upload, download, and delete operations.
/// Used for execution log body overflow (feature 0005) and report template storage (feature 0009).
/// </summary>
public partial class BlobStorageClient : IBlobStorageClient
{
    /// <summary>Response bodies up to this size are stored inline in Cosmos DB.</summary>
    public const int InlineThresholdBytes = 10 * 1024; // 10 KB

    private readonly BlobServiceClient _serviceClient;
    private readonly string _containerName;
    private readonly ILogger<BlobStorageClient> _logger;

    // Guards the one-time container creation check.  BlobStorageClient is Singleton,
    // so this flag is set once per process lifetime — subsequent uploads skip the
    // CreateIfNotExistsAsync round-trip (one extra API call per upload otherwise).
    private volatile bool _containerEnsured;

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

            if (!_containerEnsured)
            {
                await containerClient.CreateIfNotExistsAsync(
                    publicAccessType: PublicAccessType.None,
                    cancellationToken: cancellationToken);
                _containerEnsured = true;
            }

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

    /// <summary>
    /// Downloads text content from the blob identified by <paramref name="blobUri"/>.
    /// Returns null when the blob cannot be fetched.
    /// </summary>
    public virtual async Task<string?> DownloadAsync(
        string blobUri,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var blobClient = new BlobClient(new Uri(blobUri));
            var response = await blobClient.DownloadContentAsync(cancellationToken);
            var content = response.Value.Content.ToString();
            LogDownloaded(_logger, blobUri);
            return content;
        }
        catch (Exception ex)
        {
            LogDownloadFailed(_logger, blobUri, ex);
            return null;
        }
    }

    /// <summary>
    /// Deletes the blob identified by <paramref name="blobUri"/>.
    /// Returns true on success; blob-not-found is treated as success.
    /// Returns false when the deletion fails for any other reason.
    /// </summary>
    public virtual async Task<bool> DeleteAsync(
        string blobUri,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var blobClient = new BlobClient(new Uri(blobUri));
            await blobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken);
            LogDeleted(_logger, blobUri);
            return true;
        }
        catch (Exception ex)
        {
            LogDeleteFailed(_logger, blobUri, ex);
            return false;
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Response body blob uploaded: {BlobName}")]
    private static partial void LogUploaded(ILogger logger, string blobName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Blob upload failed for '{BlobName}' — response body will be truncated")]
    private static partial void LogUploadFailed(ILogger logger, string blobName, Exception ex);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Blob downloaded: {BlobUri}")]
    private static partial void LogDownloaded(ILogger logger, string blobUri);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Blob download failed for '{BlobUri}'")]
    private static partial void LogDownloadFailed(ILogger logger, string blobUri, Exception ex);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Blob deleted: {BlobUri}")]
    private static partial void LogDeleted(ILogger logger, string blobUri);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Blob deletion failed for '{BlobUri}'")]
    private static partial void LogDeleteFailed(ILogger logger, string blobUri, Exception ex);
}
