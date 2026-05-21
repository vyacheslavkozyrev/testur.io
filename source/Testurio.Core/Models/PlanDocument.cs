namespace Testurio.Core.Models;

/// <summary>
/// A subscription plan stored in the <c>Plans</c> Cosmos DB container.
/// Partition key: <c>type</c> (always <c>"plan"</c>). Id: the plan slug (e.g. <c>"test-pro"</c>).
/// </summary>
public sealed record PlanDocument
{
    public required string Id { get; init; }
    public required string Type { get; init; }
    public required string Name { get; init; }
    public required int MonthlyPrice { get; init; }
    public required int AnnualPrice { get; init; }
    public required int AnnualDiscountPercent { get; init; }
    public required bool IsPopular { get; init; }

    /// <summary>
    /// Marketing-copy feature strings displayed on the pricing page.
    /// Renamed from <c>Features</c> in feature 0046 to distinguish display copy from enforcement data.
    /// </summary>
    public required IReadOnlyList<string> DisplayFeatures { get; init; }

    /// <summary>Enforcement ceilings (project count, monthly run quota). Added in feature 0046.</summary>
    public required PlanLimits Limits { get; init; }

    /// <summary>Boolean capability flags controlling which pipeline stages are available. Added in feature 0046.</summary>
    public required PlanFeatures Features { get; init; }

    public required int SortOrder { get; init; }
}
