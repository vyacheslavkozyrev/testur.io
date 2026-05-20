using Microsoft.Azure.Cosmos;
using Testurio.Core.Enums;
using Testurio.Core.Models;
using Testurio.Core.Repositories;

namespace Testurio.Infrastructure.Cosmos;

public sealed class PlanRepository : IPlanRepository
{
    private const string PartitionKeyValue = "plan";

    /// <summary>
    /// Maps each <see cref="SubscriptionPlan"/> enum value to the Cosmos document <c>id</c> (slug).
    /// </summary>
    private static readonly IReadOnlyDictionary<SubscriptionPlan, string> PlanSlugs =
        new Dictionary<SubscriptionPlan, string>
        {
            [SubscriptionPlan.TestJunior] = "test-junior",
            [SubscriptionPlan.TestPro]    = "test-pro",
            [SubscriptionPlan.Team]       = "team",
            [SubscriptionPlan.Centurio]   = "centurio",
        };

    private readonly Container _container;

    public PlanRepository(CosmosClient cosmosClient, string databaseName)
    {
        _container = cosmosClient.GetContainer(databaseName, "Plans");
    }

    public async Task<IReadOnlyList<PlanDocument>> ListAllAsync(CancellationToken cancellationToken = default)
    {
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.type = @type ORDER BY c.sortOrder ASC")
            .WithParameter("@type", PartitionKeyValue);

        var results = new List<PlanDocument>();
        using var iterator = _container.GetItemQueryIterator<PlanDocument>(
            query,
            requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(PartitionKeyValue) });

        while (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync(cancellationToken);
            results.AddRange(page);
        }

        return results;
    }

    /// <inheritdoc />
    public async Task<PlanDocument?> GetByPlanAsync(SubscriptionPlan plan, CancellationToken cancellationToken = default)
    {
        if (!PlanSlugs.TryGetValue(plan, out var slug))
            return null;

        try
        {
            var response = await _container.ReadItemAsync<PlanDocument>(
                slug,
                new PartitionKey(PartitionKeyValue),
                cancellationToken: cancellationToken);
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }
}
