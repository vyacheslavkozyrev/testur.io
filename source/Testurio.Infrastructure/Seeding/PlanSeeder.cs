using Microsoft.Azure.Cosmos;
using Testurio.Core.Models;

namespace Testurio.Infrastructure.Seeding;

/// <summary>Abstraction for plan seeding; injectable in tests.</summary>
public interface IPlanSeeder
{
    Task SeedAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Seeds the initial <see cref="PlanDocument"/> records into the <c>Plans</c> Cosmos DB container.
/// Idempotent — skips documents that already exist so edits made via the Azure portal are preserved.
/// </summary>
public sealed class PlanSeeder : IPlanSeeder
{
    private const string PartitionKeyValue = "plan";

    private static readonly PlanDocument[] Plans =
    [
        new()
        {
            Id = "test-junior",
            Type = PartitionKeyValue,
            Name = "Test Junior",
            MonthlyPrice = 19,
            AnnualPrice = 182,
            AnnualDiscountPercent = 20,
            IsPopular = false,
            SortOrder = 0,
            Features =
            [
                "Up to 3 projects",
                "50 automated test runs / day",
                "API test execution",
                "Basic test reports",
                "Community support",
            ],
        },
        new()
        {
            Id = "test-pro",
            Type = PartitionKeyValue,
            Name = "Test Pro",
            MonthlyPrice = 49,
            AnnualPrice = 470,
            AnnualDiscountPercent = 20,
            IsPopular = true,
            SortOrder = 1,
            Features =
            [
                "Up to 10 projects",
                "Unlimited test runs",
                "API & UI end-to-end testing",
                "AI memory layer for smarter scenarios",
                "ADO & Jira report post-back",
                "Email support",
            ],
        },
        new()
        {
            Id = "team",
            Type = PartitionKeyValue,
            Name = "Team",
            MonthlyPrice = 149,
            AnnualPrice = 1430,
            AnnualDiscountPercent = 20,
            IsPopular = false,
            SortOrder = 2,
            Features =
            [
                "Unlimited projects",
                "Unlimited test runs",
                "API & UI end-to-end testing",
                "AI memory layer with cross-project sharing",
                "ADO & Jira report post-back",
                "Custom test generation prompts",
                "Priority support",
            ],
        },
        new()
        {
            Id = "centurio",
            Type = PartitionKeyValue,
            Name = "Centurio",
            MonthlyPrice = 399,
            AnnualPrice = 3830,
            AnnualDiscountPercent = 20,
            IsPopular = false,
            SortOrder = 3,
            Features =
            [
                "Unlimited projects",
                "Unlimited test runs",
                "All test types including smoke, a11y, visual",
                "Full AI memory layer with global anonymised sharing",
                "All PM tool integrations",
                "Dedicated egress IP range",
                "SLA guarantee",
                "Dedicated support engineer",
            ],
        },
    ];

    private readonly Container _container;

    public PlanSeeder(CosmosClient cosmosClient, string databaseName)
    {
        _container = cosmosClient.GetContainer(databaseName, "Plans");
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        foreach (var plan in Plans)
            await SeedPlanAsync(plan, cancellationToken);
    }

    private async Task SeedPlanAsync(PlanDocument plan, CancellationToken cancellationToken)
    {
        await _container.UpsertItemAsync(
            plan,
            new PartitionKey(PartitionKeyValue),
            cancellationToken: cancellationToken);
    }
}
