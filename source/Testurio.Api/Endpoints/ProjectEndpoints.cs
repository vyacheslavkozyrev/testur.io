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
        var updated = await projectService.UpdateAsync(userId, projectId, request, cancellationToken);
        return updated is null
            ? TypedResults.NotFound()
            : TypedResults.Ok(updated);
    }

    private static async Task<Results<NoContent, NotFound>> DeleteProjectAsync(
        string projectId,
        ClaimsPrincipal user,
        IProjectService projectService,
        CancellationToken cancellationToken)
    {
        var userId = user.GetUserId();
        var deleted = await projectService.DeleteAsync(userId, projectId, cancellationToken);
        return deleted
            ? TypedResults.NoContent()
            : TypedResults.NotFound();
    }
}

internal static class ClaimsPrincipalExtensions
{
    internal static string GetUserId(this ClaimsPrincipal user)
        => user.FindFirstValue("oid")
           ?? user.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier)
           ?? throw new UnauthorizedAccessException("User identity could not be resolved from token.");
}
