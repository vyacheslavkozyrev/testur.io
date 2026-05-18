namespace Testurio.Api.DTOs.Plans;

/// <summary>
/// Represents a subscription plan returned by GET /v1/plans.
/// Mapped from <c>PlanDocument</c> stored in the <c>Plans</c> Cosmos DB container.
/// </summary>
public sealed record PlanDefinitionDto(
    string Id,
    string Name,
    int MonthlyPrice,
    int AnnualPrice,
    int AnnualDiscountPercent,
    bool IsPopular,
    IReadOnlyList<string> Features);
