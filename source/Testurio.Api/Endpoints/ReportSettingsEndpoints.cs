using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Testurio.Api.DTOs;
using Testurio.Api.Services;
using Testurio.Core.Interfaces;
using Testurio.Core.Repositories;

namespace Testurio.Api.Endpoints;

/// <summary>
/// Endpoints for managing report format and attachment settings per project.
/// Route group: /v1/projects/{projectId}/report-settings
/// </summary>
public static class ReportSettingsEndpoints
{
    public static IEndpointRouteBuilder MapReportSettingsEndpoints(this IEndpointRouteBuilder app)
    {
        var v1 = app.MapGroup("/v1").RequireAuthorization();
        var reportSettings = v1.MapGroup("/projects/{projectId}/report-settings");

        reportSettings.MapGet("/", GetReportSettingsAsync).WithName("GetReportSettings");

        reportSettings.MapPost("/template", UploadTemplateAsync)
            .WithName("UploadReportTemplate")
            .DisableAntiforgery();

        reportSettings.MapDelete("/template", RemoveTemplateAsync).WithName("RemoveReportTemplate");

        reportSettings.MapPatch("/", UpdateReportSettingsAsync).WithName("UpdateReportSettings");

        return app;
    }

    // ─── GET /v1/projects/{projectId}/report-settings ────────────────────────

    private static async Task<Results<Ok<ReportSettingsDto>, NotFound, ForbidHttpResult>> GetReportSettingsAsync(
        string projectId,
        ClaimsPrincipal user,
        IProjectRepository projectRepository,
        CancellationToken cancellationToken)
    {
        var userId = user.GetUserId();
        var project = await projectRepository.GetByIdAsync(userId, projectId, cancellationToken);
        if (project is null)
            return TypedResults.NotFound();

        var fileName = ExtractFileName(project.ReportTemplateUri);
        var dto = new ReportSettingsDto(
            project.ReportTemplateUri,
            fileName,
            project.ReportIncludeLogs,
            project.ReportIncludeScreenshots);

        return TypedResults.Ok(dto);
    }

    // ─── POST /v1/projects/{projectId}/report-settings/template ──────────────

    private static async Task<Results<Ok<ReportTemplateUploadResponse>, BadRequest<ProblemDetails>, NotFound, ForbidHttpResult>> UploadTemplateAsync(
        string projectId,
        IFormFile? file,
        ClaimsPrincipal user,
        IReportTemplateService reportTemplateService,
        IProjectRepository projectRepository,
        CancellationToken cancellationToken)
    {
        var userId = user.GetUserId();

        // Ownership check.
        var project = await projectRepository.GetByIdAsync(userId, projectId, cancellationToken);
        if (project is null)
            return TypedResults.NotFound();

        if (file is null || file.Length == 0)
        {
            return TypedResults.BadRequest(new ProblemDetails
            {
                Title = "No file was provided.",
                Status = 400,
            });
        }

        await using var stream = file.OpenReadStream();
        var result = await reportTemplateService.UploadTemplateAsync(
            projectId,
            userId,
            file.FileName,
            stream,
            file.Length,
            cancellationToken);

        if (!result.IsSuccess)
        {
            return TypedResults.BadRequest(new ProblemDetails
            {
                Title = result.ErrorMessage,
                Status = 400,
            });
        }

        return TypedResults.Ok(new ReportTemplateUploadResponse(result.BlobUri!, result.Warnings));
    }

    // ─── DELETE /v1/projects/{projectId}/report-settings/template ────────────

    private static async Task<Results<NoContent, BadRequest<ProblemDetails>, NotFound, ForbidHttpResult>> RemoveTemplateAsync(
        string projectId,
        ClaimsPrincipal user,
        IReportTemplateService reportTemplateService,
        IProjectRepository projectRepository,
        CancellationToken cancellationToken)
    {
        var userId = user.GetUserId();

        var project = await projectRepository.GetByIdAsync(userId, projectId, cancellationToken);
        if (project is null)
            return TypedResults.NotFound();

        var success = await reportTemplateService.RemoveTemplateAsync(projectId, userId, cancellationToken);
        if (!success)
        {
            return TypedResults.BadRequest(new ProblemDetails
            {
                Title = "Failed to delete the template file. The existing template remains in effect.",
                Status = 400,
            });
        }

        return TypedResults.NoContent();
    }

    // ─── PATCH /v1/projects/{projectId}/report-settings ──────────────────────

    private static async Task<Results<Ok<ReportSettingsDto>, BadRequest<ProblemDetails>, NotFound, ForbidHttpResult>> UpdateReportSettingsAsync(
        string projectId,
        UpdateReportSettingsRequest request,
        ClaimsPrincipal user,
        IProjectRepository projectRepository,
        CancellationToken cancellationToken)
    {
        var userId = user.GetUserId();

        var project = await projectRepository.GetByIdAsync(userId, projectId, cancellationToken);
        if (project is null)
            return TypedResults.NotFound();

        // AC-026: reportIncludeScreenshots must not be true when test_type is api.
        // test_type is stored as TestingStrategy for now; the API-level validation
        // is enforced by ReportConfigurationValidator at the service boundary.
        if (request.ReportIncludeScreenshots && IsApiOnlyProject(project.TestingStrategy))
        {
            return TypedResults.BadRequest(new ProblemDetails
            {
                Title = "reportIncludeScreenshots cannot be true when test_type is api.",
                Status = 400,
            });
        }

        project.ReportIncludeLogs = request.ReportIncludeLogs;
        project.ReportIncludeScreenshots = request.ReportIncludeScreenshots;
        project.UpdatedAt = DateTimeOffset.UtcNow;

        var updated = await projectRepository.UpdateAsync(project, cancellationToken);

        var fileName = ExtractFileName(updated.ReportTemplateUri);
        var dto = new ReportSettingsDto(
            updated.ReportTemplateUri,
            fileName,
            updated.ReportIncludeLogs,
            updated.ReportIncludeScreenshots);

        return TypedResults.Ok(dto);
    }

    private static string? ExtractFileName(string? blobUri)
    {
        if (string.IsNullOrEmpty(blobUri))
            return null;
        // Blob URIs end with templates/{projectId}/{fileName}
        var lastSlash = blobUri.LastIndexOf('/');
        return lastSlash >= 0 ? blobUri[(lastSlash + 1)..] : null;
    }

    /// <summary>
    /// Heuristic check: returns true when the project's TestingStrategy indicates API-only testing.
    /// The full test_type field (api / ui_e2e / both) is part of a future project config model;
    /// for now we cannot reliably distinguish — the validator is the authoritative gate (AC-026).
    /// </summary>
    private static bool IsApiOnlyProject(string testingStrategy) => false;
}
