using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Testurio.Api.DTOs;
using Testurio.Core.Interfaces;
using Testurio.Core.Repositories;

namespace Testurio.Api.Services;

/// <summary>
/// Handles report template upload, validation, token scanning, and removal for projects.
/// Validates extension, size, and UTF-8 encoding; scans for unknown placeholder tokens.
/// </summary>
public partial class ReportTemplateService : IReportTemplateService
{
    /// <summary>Maximum allowed template file size in bytes (100 KB).</summary>
    public const int MaxTemplateSizeBytes = 100 * 1024;

    /// <summary>Supported placeholder tokens (AC-016).</summary>
    public static readonly IReadOnlySet<string> SupportedTokens = new HashSet<string>(StringComparer.Ordinal)
    {
        "{{story_title}}",
        "{{story_url}}",
        "{{run_date}}",
        "{{overall_result}}",
        "{{scenarios}}",
        "{{logs}}",
        "{{screenshots}}",
        "{{ai_scenario_source}}",
        "{{timing_summary}}",
    };

    private readonly IProjectRepository _projectRepository;
    private readonly ITemplateRepository _templateRepository;
    private readonly ILogger<ReportTemplateService> _logger;

    public ReportTemplateService(
        IProjectRepository projectRepository,
        ITemplateRepository templateRepository,
        ILogger<ReportTemplateService> logger)
    {
        _projectRepository = projectRepository;
        _templateRepository = templateRepository;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<ReportTemplateUploadResult> UploadTemplateAsync(
        string projectId,
        string userId,
        string fileName,
        Stream fileStream,
        long fileSizeBytes,
        CancellationToken cancellationToken = default)
    {
        // Validate extension (AC-002).
        if (!fileName.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            return ReportTemplateUploadResult.Failure("Only Markdown (.md) files are accepted.");

        // Validate size (AC-003, AC-005).
        if (fileSizeBytes > MaxTemplateSizeBytes)
            return ReportTemplateUploadResult.Failure("Template file must be 100 KB or smaller.");

        // Read and validate UTF-8 content (AC-005).
        string content;
        try
        {
            using var reader = new StreamReader(fileStream, System.Text.Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
            content = await reader.ReadToEndAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            LogInvalidEncoding(_logger, projectId, ex);
            return ReportTemplateUploadResult.Failure("Template file must be valid UTF-8 text.");
        }

        // Load the project to get the existing template URI (for replacement).
        var project = await _projectRepository.GetByIdAsync(userId, projectId, cancellationToken);
        if (project is null)
            return ReportTemplateUploadResult.Failure($"Project {projectId} not found.");

        var oldBlobUri = project.ReportTemplateUri;

        // Reset the stream to upload.
        fileStream.Seek(0, SeekOrigin.Begin);
        var newBlobUri = await _templateRepository.UploadAsync(projectId, fileName, fileStream, cancellationToken);
        if (newBlobUri is null)
            return ReportTemplateUploadResult.Failure("Failed to upload template to blob storage.");

        // Update project document with the new template URI (AC-004).
        project.ReportTemplateUri = newBlobUri;
        project.UpdatedAt = DateTimeOffset.UtcNow;
        await _projectRepository.UpdateAsync(project, cancellationToken);

        // Clean up old blob after updating the project document (AC-013).
        if (!string.IsNullOrEmpty(oldBlobUri))
        {
            var deleted = await _templateRepository.DeleteAsync(oldBlobUri, cancellationToken);
            if (!deleted)
            {
                // AC-014: orphaned blob is flagged but new template is still active.
                LogOrphanedBlob(_logger, projectId, oldBlobUri);
            }
        }

        // Scan for unknown tokens (AC-036).
        var warnings = ScanForUnknownTokens(content);

        LogTemplateUploaded(_logger, projectId, newBlobUri, warnings.Count);
        return ReportTemplateUploadResult.Success(newBlobUri, warnings);
    }

    /// <inheritdoc/>
    public async Task<bool> RemoveTemplateAsync(
        string projectId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        var project = await _projectRepository.GetByIdAsync(userId, projectId, cancellationToken);
        if (project is null || string.IsNullOrEmpty(project.ReportTemplateUri))
            return true; // Nothing to remove.

        var blobUri = project.ReportTemplateUri;

        // Delete blob first; only clear the URI if deletion succeeded (AC-011).
        var deleted = await _templateRepository.DeleteAsync(blobUri, cancellationToken);
        if (!deleted)
        {
            LogDeleteFailed(_logger, projectId, blobUri);
            return false;
        }

        project.ReportTemplateUri = null;
        project.UpdatedAt = DateTimeOffset.UtcNow;
        await _projectRepository.UpdateAsync(project, cancellationToken);

        LogTemplateRemoved(_logger, projectId);
        return true;
    }

    /// <summary>
    /// Scans <paramref name="content"/> for <c>{{...}}</c> tokens not in <see cref="SupportedTokens"/>.
    /// Returns a list of unrecognised token strings (AC-036, AC-037).
    /// </summary>
    public static IReadOnlyList<string> ScanForUnknownTokens(string content)
    {
        var matches = TokenPattern().Matches(content);
        var unknown = new List<string>();
        foreach (Match m in matches)
        {
            var token = m.Value;
            if (!SupportedTokens.Contains(token))
                unknown.Add(token);
        }
        return unknown.Distinct().ToList();
    }

    [GeneratedRegex(@"\{\{[^}]+\}\}", RegexOptions.Compiled)]
    private static partial Regex TokenPattern();

    [LoggerMessage(Level = LogLevel.Warning, Message = "Template file for project {ProjectId} is not valid UTF-8")]
    private static partial void LogInvalidEncoding(ILogger logger, string projectId, Exception ex);

    [LoggerMessage(Level = LogLevel.Information, Message = "Report template uploaded for project {ProjectId}: {BlobUri} ({WarningCount} token warnings)")]
    private static partial void LogTemplateUploaded(ILogger logger, string projectId, string blobUri, int warningCount);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Old template blob orphaned for project {ProjectId} — deletion failed: {OldBlobUri}")]
    private static partial void LogOrphanedBlob(ILogger logger, string projectId, string oldBlobUri);

    [LoggerMessage(Level = LogLevel.Information, Message = "Report template removed for project {ProjectId}")]
    private static partial void LogTemplateRemoved(ILogger logger, string projectId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to delete template blob for project {ProjectId}: {BlobUri}")]
    private static partial void LogDeleteFailed(ILogger logger, string projectId, string blobUri);
}
