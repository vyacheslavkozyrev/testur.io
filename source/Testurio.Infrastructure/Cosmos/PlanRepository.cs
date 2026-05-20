using Microsoft.Azure.Cosmos;
using Testurio.Core.Models;
using Testurio.Core.Repositories;

namespace Testurio.Infrastructure.Cosmos;

public sealed class PlanRepository : IPlanRepository
{
    private const string PartitionKeyValue = "plan";

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
}
