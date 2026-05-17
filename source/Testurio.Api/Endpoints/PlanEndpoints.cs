using Microsoft.AspNetCore.Http.HttpResults;
using Testurio.Api.Configuration;
using Testurio.Api.DTOs.Plans;

namespace Testurio.Api.Endpoints;

public static class PlanEndpoints
{
    /// <summary>
    /// Registers the public plans endpoint.
    /// GET /v1/plans — no authentication required; returns the static plan catalog with a 1-hour cache hint.
    /// </summary>
    public static IEndpointRouteBuilder MapPlanEndpoints(this IEndpointRouteBuilder app)
    {
        var plans = app.MapGroup("/v1/plans");

        plans.MapGet("/", GetPlansAsync)
             .WithName("GetPlans")
             .AllowAnonymous();

        return app;
    }

    private static Ok<IReadOnlyList<PlanDefinitionDto>> GetPlansAsync(HttpResponse response)
    {
        response.Headers.CacheControl = "public, max-age=3600";
        return TypedResults.Ok(PlanCatalog.All);
    }
}
