namespace Testurio.Core.Interfaces;

/// <summary>
/// Result of a report template upload operation.
/// </summary>
public record ReportTemplateUploadResult(
    bool IsSuccess,
    string? BlobUri,
    IReadOnlyList<string> Warnings,
    string? ErrorMessage)
{
    public static ReportTemplateUploadResult Success(string blobUri, IReadOnlyList<string> warnings) =>
        new(true, blobUri, warnings, null);

    public static ReportTemplateUploadResult Failure(string errorMessage) =>
        new(false, null, [], errorMessage);
}

/// <summary>
/// Service for managing project report templates stored in Azure Blob Storage.
/// </summary>
public interface IReportTemplateService
{
    /// <summary>
    /// Validates and uploads a report template file for the given project.
    /// If the project already has a template, the old blob is replaced.
    /// Returns the blob URI and any token warnings on success.
    /// </summary>
    Task<ReportTemplateUploadResult> UploadTemplateAsync(
        string projectId,
        string userId,
        string fileName,
        Stream fileStream,
        long fileSizeBytes,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes the report template for the given project.
    /// Deletes the blob and clears <c>reportTemplateUri</c> on the project document.
    /// Returns false when the blob deletion fails (project document is not modified in that case).
    /// </summary>
    Task<bool> RemoveTemplateAsync(
        string projectId,
        string userId,
        CancellationToken cancellationToken = default);
}
