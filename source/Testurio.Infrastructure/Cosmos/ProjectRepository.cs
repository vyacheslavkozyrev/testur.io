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
            var response = await _container.ReadItemAsync<Project>(
                projectId,
                new PartitionKey(userId),
                cancellationToken: cancellationToken);
            var project = response.Resource;
            return project.IsDeleted ? null : project;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<IReadOnlyList<Project>> ListByUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.userId = @userId AND (NOT IS_DEFINED(c.isDeleted) OR c.isDeleted = false)")
            .WithParameter("@userId", userId);

        var results = new List<Project>();
        using var iterator = _container.GetItemQueryIterator<Project>(
            query,
            requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(userId) });

        while (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync(cancellationToken);
            results.AddRange(page);
        }

        return results;
    }

    public async Task<Project> CreateAsync(Project project, CancellationToken cancellationToken = default)
    {
        var response = await _container.CreateItemAsync(
            project,
            new PartitionKey(project.UserId),
            cancellationToken: cancellationToken);
        return response.Resource;
    }

    public async Task<Project> UpdateAsync(Project project, CancellationToken cancellationToken = default)
    {
        var response = await _container.ReplaceItemAsync(
            project,
            project.Id,
            new PartitionKey(project.UserId),
            cancellationToken: cancellationToken);
        return response.Resource;
    }

    // Cross-partition fan-out: called from the HMAC filter before userId is known — intentional trade-off
    // for the webhook auth path. MaxItemCount = 1 caps the first page to one document per partition.
    // Note: EnableScanInQuery controls in-partition index scans, not cross-partition fan-out RU cost.
    // A dedicated indexing policy on 'id' or a lookup document keyed by projectId would eliminate the fan-out.
    public async Task<Project?> GetByProjectIdAsync(string projectId, CancellationToken cancellationToken = default)
    {
        using var query = _container.GetItemLinqQueryable<Project>(allowSynchronousQueryExecution: false,
            requestOptions: new QueryRequestOptions { MaxItemCount = 1 })
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
