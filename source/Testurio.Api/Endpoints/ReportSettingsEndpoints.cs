using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Testurio.Api.DTOs;
using Testurio.Api.Services;
using Testurio.Api.Validators;
using Testurio.Core.Interfaces;
using Testurio.Core.Repositories;

namespace Testurio.Api.Endpoints;

/// <summary>
/// Endpoints for managing report format and attachment settings per project.
/// Route group: /v1/projects/{projectId}/report-settings
/// </summary>
public static class ReportSettingsEndpoints
{
    public static IEndpointRouteBuilder MapReportSettingsEndpoints(this IEndpointRouteBuilder app, RouteGroupBuilder v1)
    {
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
        [FromForm] IFormFile? file,
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
            var vpd = new ValidationProblemDetails(
                new Dictionary<string, string[]> { ["file"] = ["No file was provided."] })
            { Status = 400 };
            return TypedResults.BadRequest<ProblemDetails>(vpd);
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
            var vpd = new ValidationProblemDetails(
                new Dictionary<string, string[]> { ["file"] = [result.ErrorMessage ?? string.Empty] })
            { Status = 400 };
            return TypedResults.BadRequest<ProblemDetails>(vpd);
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
        var validationErrors = ReportConfigurationValidator.Validate(request, project.TestingStrategy).ToList();
        if (validationErrors.Count > 0)
        {
            var vpd = new ValidationProblemDetails();
            foreach (var ve in validationErrors)
            {
                foreach (var member in ve.MemberNames)
                    vpd.Errors[member] = [ve.ErrorMessage ?? string.Empty];
            }
            vpd.Status = 400;
            return TypedResults.BadRequest<ProblemDetails>(vpd);
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
        return Path.GetFileName(new Uri(blobUri).LocalPath);
    }

}
