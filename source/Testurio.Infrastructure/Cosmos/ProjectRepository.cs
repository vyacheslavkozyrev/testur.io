using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Testurio.Core.Entities;
using Testurio.Core.Repositories;

namespace Testurio.Infrastructure.Cosmos;

public class ProjectRepository : IProjectRepository
{
    private readonly Container _container;

    public ProjectRepository(CosmosClient cosmosClient, string databaseName)
    {
        _container = cosmosClient.GetContainer(databaseName, "Projects");
    }

    public async Task<Project?> GetByIdAsync(string userId, string projectId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _container.ReadItemAsync<Project>(projectId, new PartitionKey(userId), cancellationToken: cancellationToken);
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    // Cross-partition query: called from the HMAC filter before userId is known — intentional trade-off
    // for the webhook auth path. MaxItemCount = 1 limits RU cost to a single-document scan.
    public async Task<Project?> GetByProjectIdAsync(string projectId, CancellationToken cancellationToken = default)
    {
        var query = _container.GetItemLinqQueryable<Project>(allowSynchronousQueryExecution: false,
            requestOptions: new QueryRequestOptions { MaxItemCount = 1, EnableScanInQuery = false })
            .Where(p => p.Id == projectId)
            .ToFeedIterator();

        while (query.HasMoreResults)
        {
            var page = await query.ReadNextAsync(cancellationToken);
            var result = page.FirstOrDefault();
            if (result is not null) return result;
        }

        return null;
    }
}
