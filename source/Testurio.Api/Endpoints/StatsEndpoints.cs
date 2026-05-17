using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http.HttpResults;
using Testurio.Api.DTOs;
using Testurio.Api.Services;
using Testurio.Core.Interfaces;

namespace Testurio.Api.Endpoints;

public static class StatsEndpoints
{
    private static readonly JsonSerializerOptions SseJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
    };

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

        // Feature 0043: SSE stream — must not be discovered by OpenAPI schema generators as a normal JSON endpoint.
        stats.MapGet("/dashboard/stream", StreamDashboardAsync)
             .WithName("StreamDashboard")
             .ExcludeFromDescription();

        stats.MapGet("/projects/{projectId:guid}/history", GetProjectHistoryAsync)
             .WithName("GetProjectHistory");

        stats.MapGet("/projects/{projectId:guid}/runs/{runId:guid}", GetRunDetailAsync)
             .WithName("GetRunDetail");

        return app;
    }

    /// <summary>
    /// SSE endpoint — streams <see cref="Testurio.Core.Events.DashboardUpdatedEvent"/> JSON objects
    /// to the authenticated client until the connection is cancelled.
    /// Each event is written as a <c>data:</c> SSE line followed by a blank line.
    /// </summary>
    private static async Task StreamDashboardAsync(
        ClaimsPrincipal user,
        IDashboardStreamManager streamManager,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var userId = user.GetUserId();

        httpContext.Response.Headers.ContentType = "text/event-stream";
        httpContext.Response.Headers.CacheControl = "no-cache";
        httpContext.Response.Headers.Connection = "keep-alive";

        // Flush headers immediately so the client knows the connection is open.
        await httpContext.Response.Body.FlushAsync(cancellationToken);

        await foreach (var @event in streamManager.StreamAsync(userId, cancellationToken))
        {
            var json = JsonSerializer.Serialize(@event, SseJsonOptions);
            await httpContext.Response.WriteAsync($"data: {json}\n\n", cancellationToken);
            await httpContext.Response.Body.FlushAsync(cancellationToken);
        }
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
