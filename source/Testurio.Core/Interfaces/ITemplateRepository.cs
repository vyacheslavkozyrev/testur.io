namespace Testurio.Core.Interfaces;

/// <summary>
/// Storage abstraction for report template blobs.
/// </summary>
public interface ITemplateRepository
{
    /// <summary>
    /// Uploads a template file for <paramref name="projectId"/> and returns its blob URI.
    /// Returns null when the upload fails.
    /// </summary>
    Task<string?> UploadAsync(string projectId, string fileName, Stream content, CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads the template text from <paramref name="blobUri"/>.
    /// Returns null when the download fails.
    /// </summary>
    Task<string?> DownloadAsync(string blobUri, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes the blob at <paramref name="blobUri"/>.
    /// Returns true on success (including blob-not-found); false on unexpected errors.
    /// </summary>
    Task<bool> DeleteAsync(string blobUri, CancellationToken cancellationToken = default);
}
