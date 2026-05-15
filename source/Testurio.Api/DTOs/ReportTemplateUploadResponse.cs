namespace Testurio.Api.DTOs;

/// <summary>
/// Response returned after a successful report template upload.
/// </summary>
public record ReportTemplateUploadResponse(
    string BlobUri,
    IReadOnlyList<string> Warnings);
