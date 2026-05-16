using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Testurio.Api.DTOs;
using Testurio.Api.Middleware;
using Testurio.Api.Services;

namespace Testurio.Api.Endpoints;

public static class ProjectAccessEndpoints
{
    public static IEndpointRouteBuilder MapProjectAccessEndpoints(this IEndpointRouteBuilder app)
    {
        var v1 = app.MapGroup("/v1").RequireAuthorization();
        var projects = v1.MapGroup("/projects");

        projects.MapGet("/{projectId}/access", GetProjectAccessAsync).WithName("GetProjectAccess");
        projects.MapPatch("/{projectId}/access", UpdateProjectAccessAsync).WithName("UpdateProjectAccess")
            .AddEndpointFilter<ValidationFilter<UpdateProjectAccessRequest>>();

        return app;
    }

    private static async Task<Results<Ok<ProjectAccessDto>, NotFound, ForbidHttpResult>> GetProjectAccessAsync(
        string projectId,
        ClaimsPrincipal user,
        IProjectAccessService projectAccessService,
        CancellationToken cancellationToken)
    {
        var userId = user.GetUserId();
        var (result, dto) = await projectAccessService.GetAsync(userId, projectId, cancellationToken);
        return result switch
        {
            ProjectOperationResult.Forbidden => TypedResults.Forbid(),
            ProjectOperationResult.NotFound  => TypedResults.NotFound(),
            _                                => TypedResults.Ok(dto!),
        };
    }

    private static async Task<Results<Ok<ProjectAccessDto>, NotFound, ForbidHttpResult>> UpdateProjectAccessAsync(
        string projectId,
        UpdateProjectAccessRequest request,
        ClaimsPrincipal user,
        IProjectAccessService projectAccessService,
        CancellationToken cancellationToken)
    {
        var userId = user.GetUserId();
        var (result, dto) = await projectAccessService.UpdateAsync(userId, projectId, request, cancellationToken);
        return result switch
        {
            ProjectOperationResult.Forbidden => TypedResults.Forbid(),
            ProjectOperationResult.NotFound  => TypedResults.NotFound(),
            _                                => TypedResults.Ok(dto!),
        };
    }
}
