using System.ComponentModel.DataAnnotations;

namespace Testurio.Api.DTOs;

/// <summary>
/// Multipart form data model for uploading a Markdown report template.
/// </summary>
public record ReportTemplateUploadRequest
{
    /// <summary>The uploaded .md file.</summary>
    [Required]
    public required IFormFile File { get; init; }
}
