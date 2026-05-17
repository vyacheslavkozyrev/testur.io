using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Testurio.Api.DTOs;
using Testurio.Api.Services;

namespace Testurio.Api.Endpoints;

public static class StatsEndpoints
{
    /// <summary>
    /// Registers all stats-related endpoints under <c>/v1/stats</c>.
    /// Feature 0043 adds the SSE endpoint to this same route group.
    /// </summary>
    public static IEndpointRouteBuilder MapStatsEndpoints(
        this IEndpointRouteBuilder app,
        RouteGroupBuilder v1)
    {
        var stats = v1.MapGroup("/stats");

        stats.MapGet("/dashboard", GetDashboardAsync).WithName("GetDashboard");

        stats.MapGet("/projects/{projectId:guid}/history", GetProjectHistoryAsync)
             .WithName("GetProjectHistory");

        stats.MapGet("/projects/{projectId:guid}/runs/{runId:guid}", GetRunDetailAsync)
             .WithName("GetRunDetail");

        return app;
    }

    private static async Task<Ok<DashboardResponse>> GetDashboardAsync(
        ClaimsPrincipal user,
        IDashboardService dashboardService,
        CancellationToken cancellationToken)
    {
        var userId = user.GetUserId();
        var response = await dashboardService.GetDashboardAsync(userId, cancellationToken);
        return TypedResults.Ok(response);
    }

    private static async Task<Results<Ok<ProjectHistoryResponse>, NotFound>> GetProjectHistoryAsync(
        Guid projectId,
        ClaimsPrincipal user,
        IProjectHistoryService projectHistoryService,
        CancellationToken cancellationToken)
    {
        var userId = user.GetUserId();
        var response = await projectHistoryService.GetHistoryAsync(userId, projectId.ToString(), cancellationToken);
        return response is not null
            ? TypedResults.Ok(response)
            : TypedResults.NotFound();
    }

    private static async Task<Results<Ok<RunDetailResponse>, NotFound>> GetRunDetailAsync(
        Guid projectId,
        Guid runId,
        ClaimsPrincipal user,
        IProjectHistoryService projectHistoryService,
        CancellationToken cancellationToken)
    {
        var userId = user.GetUserId();
        var response = await projectHistoryService.GetRunDetailAsync(userId, projectId.ToString(), runId.ToString(), cancellationToken);
        return response is not null
            ? TypedResults.Ok(response)
            : TypedResults.NotFound();
    }
}
