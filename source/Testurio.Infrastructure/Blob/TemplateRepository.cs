using System.Text;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Logging;
using Testurio.Core.Interfaces;

namespace Testurio.Infrastructure.Blob;

/// <summary>
/// Stores and retrieves report template blobs in a dedicated Azure Blob Storage container.
/// Blob names follow the pattern <c>templates/{projectId}/{fileName}</c> so templates are
/// isolated per project and the file name is visible in the container for operator inspection.
/// </summary>
public partial class TemplateRepository : ITemplateRepository
{
    private readonly BlobServiceClient _serviceClient;
    private readonly string _containerName;
    private readonly ILogger<TemplateRepository> _logger;

    private readonly SemaphoreSlim _containerInitLock = new(1, 1);
    private bool _containerEnsured;

    public TemplateRepository(
        BlobServiceClient serviceClient,
        string containerName,
        ILogger<TemplateRepository> logger)
    {
        _serviceClient = serviceClient;
        _containerName = containerName;
        _logger = logger;
    }

    /// <summary>
    /// Uploads a template file for <paramref name="projectId"/> and returns its blob URI.
    /// Returns null when the upload fails.
    /// </summary>
    public async Task<string?> UploadAsync(
        string projectId,
        string fileName,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var containerClient = await EnsureContainerAsync(cancellationToken);
            var blobName = $"templates/{projectId}/{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}-{fileName}";
            var blobClient = containerClient.GetBlobClient(blobName);
            await blobClient.UploadAsync(content, overwrite: true, cancellationToken: cancellationToken);
            LogUploaded(_logger, blobName, projectId);
            return blobClient.Uri.ToString();
        }
        catch (Exception ex)
        {
            LogUploadFailed(_logger, projectId, ex);
            return null;
        }
    }

    /// <summary>
    /// Downloads the template text from <paramref name="blobUri"/>.
    /// Routes through the authenticated <see cref="BlobServiceClient"/> to avoid 401 in production.
    /// Returns null when the download fails.
    /// </summary>
    public async Task<string?> DownloadAsync(
        string blobUri,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var blobName = Path.GetFileName(new Uri(blobUri).LocalPath);
            var containerClient = _serviceClient.GetBlobContainerClient(_containerName);
            var blobClient = containerClient.GetBlobClient(blobName);
            var response = await blobClient.DownloadContentAsync(cancellationToken);
            var text = Encoding.UTF8.GetString(response.Value.Content);
            LogDownloaded(_logger, blobUri);
            return text;
        }
        catch (Exception ex)
        {
            LogDownloadFailed(_logger, blobUri, ex);
            return null;
        }
    }

    /// <summary>
    /// Deletes the blob at <paramref name="blobUri"/>.
    /// Routes through the authenticated <see cref="BlobServiceClient"/> to avoid 401 in production.
    /// Returns true on success (including blob-not-found); false on unexpected errors.
    /// </summary>
    public async Task<bool> DeleteAsync(
        string blobUri,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var blobName = Path.GetFileName(new Uri(blobUri).LocalPath);
            var containerClient = _serviceClient.GetBlobContainerClient(_containerName);
            var blobClient = containerClient.GetBlobClient(blobName);
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

    private async Task<BlobContainerClient> EnsureContainerAsync(CancellationToken cancellationToken)
    {
        var containerClient = _serviceClient.GetBlobContainerClient(_containerName);
        if (_containerEnsured)
            return containerClient;

        await _containerInitLock.WaitAsync(cancellationToken);
        try
        {
            if (!_containerEnsured)
            {
                await containerClient.CreateIfNotExistsAsync(
                    publicAccessType: PublicAccessType.None,
                    cancellationToken: cancellationToken);
                _containerEnsured = true;
            }
        }
        finally
        {
            _containerInitLock.Release();
        }

        return containerClient;
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Report template blob uploaded: {BlobName} for project {ProjectId}")]
    private static partial void LogUploaded(ILogger logger, string blobName, string projectId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Report template upload failed for project {ProjectId}")]
    private static partial void LogUploadFailed(ILogger logger, string projectId, Exception ex);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Report template downloaded: {BlobUri}")]
    private static partial void LogDownloaded(ILogger logger, string blobUri);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Report template download failed for '{BlobUri}'")]
    private static partial void LogDownloadFailed(ILogger logger, string blobUri, Exception ex);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Report template blob deleted: {BlobUri}")]
    private static partial void LogDeleted(ILogger logger, string blobUri);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Report template blob deletion failed for '{BlobUri}'")]
    private static partial void LogDeleteFailed(ILogger logger, string blobUri, Exception ex);
}
