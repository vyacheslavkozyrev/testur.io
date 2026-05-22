using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Testurio.Api.DTOs;
using Testurio.Api.Services;

namespace Testurio.Api.Endpoints;

/// <summary>
/// Admin endpoints for reading and updating pipeline prompt templates.
/// Both endpoints require the <c>admin</c> role claim in the caller's JWT.
/// </summary>
public static class AdminPromptTemplateEndpoints
{
    /// <summary>
    /// Valid MVP stage keys. Any request with a stage key not in this set returns 400.
    /// </summary>
    private static readonly HashSet<string> ValidStages = new(StringComparer.OrdinalIgnoreCase)
    {
        "story_parser",
        "agent_router",
        "api_test_generator",
        "ui_e2e_test_generator",
        "report_writer"
    };

    public static IEndpointRouteBuilder MapAdminPromptTemplateEndpoints(
        this IEndpointRouteBuilder app,
        RouteGroupBuilder adminGroup)
    {
        var promptTemplates = adminGroup.MapGroup("/prompt-templates");

        promptTemplates.MapGet("/{stage}", GetPromptTemplate)
            .WithName("GetAdminPromptTemplate");

        promptTemplates.MapPut("/{stage}", UpdatePromptTemplate)
            .WithName("UpdateAdminPromptTemplate");

        return app;
    }

    private static async Task<Results<Ok<PromptTemplateDto>, NotFound, BadRequest<ProblemDetails>>> GetPromptTemplate(
        string stage,
        IPromptTemplateAdminService adminService,
        CancellationToken ct)
    {
        if (!ValidStages.Contains(stage))
        {
            return TypedResults.BadRequest(new ProblemDetails
            {
                Status = 400,
                Title = "Invalid stage key",
                Detail = $"'{stage}' is not a valid pipeline stage key. Valid values: {string.Join(", ", ValidStages)}."
            });
        }

        var dto = await adminService.GetAsync(stage, ct);
        if (dto is null)
            return TypedResults.NotFound();

        return TypedResults.Ok(dto);
    }

    private static async Task<Results<Ok<PromptTemplateDto>, NotFound, BadRequest<ProblemDetails>>> UpdatePromptTemplate(
        string stage,
        [FromBody] UpdatePromptTemplateRequest request,
        IPromptTemplateAdminService adminService,
        CancellationToken ct)
    {
        if (!ValidStages.Contains(stage))
        {
            return TypedResults.BadRequest(new ProblemDetails
            {
                Status = 400,
                Title = "Invalid stage key",
                Detail = $"'{stage}' is not a valid pipeline stage key. Valid values: {string.Join(", ", ValidStages)}."
            });
        }

        if (string.IsNullOrWhiteSpace(request.Body))
        {
            return TypedResults.BadRequest(new ProblemDetails
            {
                Status = 400,
                Title = "Invalid request",
                Detail = "The 'body' field must be a non-empty string."
            });
        }

        try
        {
            var updated = await adminService.UpdateAsync(stage, request.Body, ct);
            return TypedResults.Ok(updated);
        }
        catch (InvalidOperationException)
        {
            return TypedResults.NotFound();
        }
    }
}
