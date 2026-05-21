using Microsoft.AspNetCore.Http.HttpResults;
using Testurio.Api.DTOs.Plans;
using Testurio.Core.Models;
using Testurio.Core.Repositories;

namespace Testurio.Api.Endpoints;

public static class PlanEndpoints
{
    public static IEndpointRouteBuilder MapPlanEndpoints(this IEndpointRouteBuilder v1)
    {
        var plans = v1.MapGroup("/plans");

        plans.MapGet("/", GetPlansAsync)
             .WithName("GetPlans")
             .AllowAnonymous();

        return v1;
    }

    private static async Task<Ok<IReadOnlyList<PlanDefinitionDto>>> GetPlansAsync(
        IPlanRepository repository,
        HttpResponse response,
        CancellationToken ct)
    {
        response.Headers.CacheControl = "public, max-age=3600";
        var documents = await repository.ListAllAsync(ct);
        var dtos = documents.Select(ToDto).ToList();
        return TypedResults.Ok<IReadOnlyList<PlanDefinitionDto>>(dtos);
    }

    private static PlanDefinitionDto ToDto(PlanDocument doc) => new(
        doc.Id,
        doc.Name,
        doc.MonthlyPrice,
        doc.AnnualPrice,
        doc.AnnualDiscountPercent,
        doc.IsPopular,
        doc.DisplayFeatures,
        new PlanLimitsDto(doc.Limits.MaxProjects, doc.Limits.MaxTestRunsPerMonth),
        new PlanFeaturesDto(doc.Features.ApiTesting, doc.Features.UiE2eTesting, doc.Features.AiMemory, doc.Features.PmReportPostBack));
}
