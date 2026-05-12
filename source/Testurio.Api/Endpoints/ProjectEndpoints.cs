using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Testurio.Api.DTOs;
using Testurio.Api.Middleware;
using Testurio.Api.Services;

namespace Testurio.Api.Endpoints;

public static class ProjectEndpoints
{
    public static IEndpointRouteBuilder MapProjectEndpoints(this IEndpointRouteBuilder app)
    {
        var v1 = app.MapGroup("/v1").RequireAuthorization();
        var projects = v1.MapGroup("/projects");

        projects.MapGet("/", ListProjectsAsync).WithName("ListProjects");
        projects.MapGet("/{projectId}", GetProjectAsync).WithName("GetProject");
        projects.MapPost("/", CreateProjectAsync).WithName("CreateProject")
            .AddEndpointFilter<ValidationFilter<CreateProjectRequest>>();
        projects.MapPut("/{projectId}", UpdateProjectAsync).WithName("UpdateProject")
            .AddEndpointFilter<ValidationFilter<UpdateProjectRequest>>();
        projects.MapDelete("/{projectId}", DeleteProjectAsync).WithName("DeleteProject");
        projects.MapPost("/{projectId}/prompt-check", PromptCheckAsync).WithName("PromptCheck")
            .AddEndpointFilter<ValidationFilter<PromptCheckRequest>>();

        return app;
    }

    private static async Task<Ok<IReadOnlyList<ProjectDto>>> ListProjectsAsync(
        ClaimsPrincipal user,
        IProjectService projectService,
        CancellationToken cancellationToken)
    {
        var userId = user.GetUserId();
        var result = await projectService.ListAsync(userId, cancellationToken);
        return TypedResults.Ok(result);
    }

    private static async Task<Results<Ok<ProjectDto>, NotFound>> GetProjectAsync(
        string projectId,
        ClaimsPrincipal user,
        IProjectService projectService,
        CancellationToken cancellationToken)
    {
        var userId = user.GetUserId();
        var project = await projectService.GetAsync(userId, projectId, cancellationToken);
        return project is null
            ? TypedResults.NotFound()
            : TypedResults.Ok(project);
    }

    private static async Task<Created<ProjectDto>> CreateProjectAsync(
        CreateProjectRequest request,
        ClaimsPrincipal user,
        IProjectService projectService,
        CancellationToken cancellationToken)
    {
        var userId = user.GetUserId();
        var created = await projectService.CreateAsync(userId, request, cancellationToken);
        return TypedResults.Created($"/v1/projects/{created.ProjectId}", created);
    }

    private static async Task<Results<Ok<ProjectDto>, NotFound, ForbidHttpResult>> UpdateProjectAsync(
        string projectId,
        UpdateProjectRequest request,
        ClaimsPrincipal user,
        IProjectService projectService,
        CancellationToken cancellationToken)
    {
        var userId = user.GetUserId();
        var (result, dto) = await projectService.UpdateAsync(userId, projectId, request, cancellationToken);
        return result switch
        {
            ProjectOperationResult.Forbidden => TypedResults.Forbid(),
            ProjectOperationResult.NotFound  => TypedResults.NotFound(),
            _                                => TypedResults.Ok(dto!),
        };
    }

    private static async Task<Results<NoContent, NotFound, ForbidHttpResult>> DeleteProjectAsync(
        string projectId,
        ClaimsPrincipal user,
        IProjectService projectService,
        CancellationToken cancellationToken)
    {
        var userId = user.GetUserId();
        var result = await projectService.DeleteAsync(userId, projectId, cancellationToken);
        return result switch
        {
            ProjectOperationResult.Forbidden => TypedResults.Forbid(),
            ProjectOperationResult.NotFound  => TypedResults.NotFound(),
            _                                => TypedResults.NoContent(),
        };
    }

    private static async Task<Results<Ok<PromptCheckFeedback>, NotFound, ForbidHttpResult>> PromptCheckAsync(
        string projectId,
        PromptCheckRequest request,
        ClaimsPrincipal user,
        IProjectService projectService,
        IPromptCheckService promptCheckService,
        CancellationToken cancellationToken)
    {
        var userId = user.GetUserId();

        // Ownership guard — distinguish "not found" from "forbidden" (AC-029).
        var (result, project) = await projectService.GetWithOwnershipCheckAsync(userId, projectId, cancellationToken);
        if (result == ProjectOperationResult.Forbidden)
            return TypedResults.Forbid();
        if (result == ProjectOperationResult.NotFound || project is null)
            return TypedResults.NotFound();

        var feedback = await promptCheckService.CheckAsync(request.CustomPrompt, project.TestingStrategy, cancellationToken);
        return TypedResults.Ok(feedback);
    }
}

internal static class ClaimsPrincipalExtensions
{
    internal static string GetUserId(this ClaimsPrincipal user)
        => user.FindFirstValue("oid")
           ?? user.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier)
           ?? throw new UnauthorizedAccessException("User identity could not be resolved from token.");
}
