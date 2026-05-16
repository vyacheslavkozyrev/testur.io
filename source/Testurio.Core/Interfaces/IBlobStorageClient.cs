namespace Testurio.Core.Interfaces;

/// <summary>
/// Abstraction over Azure Blob Storage for uploading, downloading, and deleting blobs.
/// </summary>
/// <remarks>
/// Addressing convention:
/// <list type="bullet">
///   <item><description>
///     <see cref="UploadAsync"/> accepts a <c>blobName</c> — a relative path within the container
///     (e.g. <c>"logs/{runId}/{stepIndex}.txt"</c> or <c>"reports/{projectId}/{runId}/report.md"</c>).
///     The method returns the absolute blob URI on success.
///   </description></item>
///   <item><description>
///     <see cref="DownloadAsync"/> and <see cref="DeleteAsync"/> accept a <c>blobUri</c> — the
///     absolute URI previously returned by <see cref="UploadAsync"/>
///     (e.g. <c>"https://account.blob.core.windows.net/container/logs/…"</c>).
///   </description></item>
/// </list>
/// </remarks>
public interface IBlobStorageClient
{
    /// <summary>
    /// Uploads <paramref name="content"/> as a blob named <paramref name="blobName"/> (relative path
    /// within the container) and returns the absolute blob URI on success.
    /// Returns null when the upload fails.
    /// </summary>
    Task<string?> UploadAsync(string blobName, string content, CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads text content from the blob identified by <paramref name="blobUri"/> (absolute URI).
    /// Returns null when the blob cannot be fetched.
    /// </summary>
    Task<string?> DownloadAsync(string blobUri, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes the blob identified by <paramref name="blobUri"/> (absolute URI).
    /// Returns true on success; false if the deletion failed (blob not found is treated as success).
    /// </summary>
    Task<bool> DeleteAsync(string blobUri, CancellationToken cancellationToken = default);
}
