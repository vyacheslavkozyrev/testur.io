namespace Testurio.Core.Interfaces;

/// <summary>
/// Abstraction over Azure Blob Storage for uploading, downloading, and deleting blobs.
/// </summary>
public interface IBlobStorageClient
{
    /// <summary>
    /// Uploads <paramref name="content"/> as a blob and returns its URL.
    /// Returns null when the upload fails.
    /// </summary>
    Task<string?> UploadAsync(string blobName, string content, CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads text content from the blob identified by <paramref name="blobUri"/>.
    /// Returns null when the blob cannot be fetched.
    /// </summary>
    Task<string?> DownloadAsync(string blobUri, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes the blob identified by <paramref name="blobUri"/>.
    /// Returns true on success; false if the deletion failed (blob not found is treated as success).
    /// </summary>
    Task<bool> DeleteAsync(string blobUri, CancellationToken cancellationToken = default);
}
