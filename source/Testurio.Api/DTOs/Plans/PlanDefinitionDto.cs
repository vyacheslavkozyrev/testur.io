namespace Testurio.Api.DTOs.Plans;

/// <summary>
/// Represents a subscription plan returned by GET /v1/plans.
/// Plan definitions are configuration constants — no database read required.
/// </summary>
public sealed record PlanDefinitionDto(
    string Id,
    string Name,
    int MonthlyPrice,
    int AnnualPrice,
    int AnnualDiscountPercent,
    bool IsPopular,
    IReadOnlyList<string> Features);
