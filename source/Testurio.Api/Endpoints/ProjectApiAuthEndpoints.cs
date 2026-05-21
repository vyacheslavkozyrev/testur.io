using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Testurio.Api.DTOs;
using Testurio.Api.Middleware;
using Testurio.Api.Services;

namespace Testurio.Api.Endpoints;

public static class ProjectApiAuthEndpoints
{
    public static IEndpointRouteBuilder MapProjectApiAuthEndpoints(this IEndpointRouteBuilder app, RouteGroupBuilder v1)
    {
        var projects = v1.MapGroup("/projects");

        projects.MapGet("/{projectId}/api-auth", GetProjectApiAuthAsync).WithName("GetProjectApiAuth");
        projects.MapPatch("/{projectId}/api-auth", UpdateProjectApiAuthAsync).WithName("UpdateProjectApiAuth")
            .AddEndpointFilter<ValidationFilter<UpdateProjectApiAuthRequest>>();

        return app;
    }

    private static async Task<Results<Ok<ProjectApiAuthDto>, NotFound, ForbidHttpResult>> GetProjectApiAuthAsync(
        string projectId,
        ClaimsPrincipal user,
        IProjectApiAuthService projectApiAuthService,
        CancellationToken cancellationToken)
    {
        var userId = user.GetUserId();
        var (result, dto) = await projectApiAuthService.GetAsync(userId, projectId, cancellationToken);
        return result switch
        {
            ProjectOperationResult.Forbidden => TypedResults.Forbid(),
            ProjectOperationResult.NotFound => TypedResults.NotFound(),
            _ => TypedResults.Ok(dto!),
        };
    }

    private static async Task<Results<Ok<ProjectApiAuthDto>, NotFound, ForbidHttpResult>> UpdateProjectApiAuthAsync(
        string projectId,
        UpdateProjectApiAuthRequest request,
        ClaimsPrincipal user,
        IProjectApiAuthService projectApiAuthService,
        CancellationToken cancellationToken)
    {
        var userId = user.GetUserId();
        var (result, dto) = await projectApiAuthService.UpdateAsync(userId, projectId, request, cancellationToken);
        return result switch
        {
            ProjectOperationResult.Forbidden => TypedResults.Forbid(),
            ProjectOperationResult.NotFound => TypedResults.NotFound(),
            _ => TypedResults.Ok(dto!),
        };
    }
}
