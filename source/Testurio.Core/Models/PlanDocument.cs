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
    public required IReadOnlyList<string> Features { get; init; }
    public required int SortOrder { get; init; }
}
