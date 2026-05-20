namespace Testurio.Api.DTOs.Plans;

/// <summary>
/// Enforcement ceilings returned as part of <see cref="PlanDefinitionDto"/>.
/// </summary>
public sealed record PlanLimitsDto(
    int MaxProjects,
    int MaxTestRunsPerMonth);

/// <summary>
/// Boolean capability flags returned as part of <see cref="PlanDefinitionDto"/>.
/// </summary>
public sealed record PlanFeaturesDto(
    bool ApiTesting,
    bool UiE2eTesting,
    bool AiMemory,
    bool PmReportPostBack);

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
    IReadOnlyList<string> DisplayFeatures,
    PlanLimitsDto Limits,
    PlanFeaturesDto Features);
