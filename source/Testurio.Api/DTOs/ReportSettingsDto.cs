namespace Testurio.Api.DTOs;

/// <summary>
/// Represents the current report settings for a project.
/// </summary>
public record ReportSettingsDto(
    string? ReportTemplateUri,
    string? ReportTemplateFileName,
    bool ReportIncludeLogs,
    bool ReportIncludeScreenshots);

/// <summary>
/// Request body for PATCH /v1/projects/{id}/report-settings — updates attachment toggles only.
/// </summary>
public record UpdateReportSettingsRequest(
    bool ReportIncludeLogs,
    bool ReportIncludeScreenshots);
