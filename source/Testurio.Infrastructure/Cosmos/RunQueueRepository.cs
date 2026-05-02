using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Testurio.Core.Entities;
using Testurio.Core.Repositories;

namespace Testurio.Infrastructure.Cosmos;

public class RunQueueRepository : IRunQueueRepository
{
    private readonly Container _container;

    public RunQueueRepository(CosmosClient cosmosClient, string databaseName)
    {
        _container = cosmosClient.GetContainer(databaseName, "RunQueue");
    }

    public async Task<IReadOnlyList<QueuedRun>> GetQueueAsync(string projectId, CancellationToken cancellationToken = default)
    {
        var results = new List<QueuedRun>();
        var query = _container.GetItemLinqQueryable<QueuedRun>(requestOptions: new QueryRequestOptions
        {
            PartitionKey = new PartitionKey(projectId)
        })
        .Where(r => r.ProjectId == projectId)
        .OrderBy(r => r.QueuedAt)
        .ToFeedIterator();

        while (query.HasMoreResults)
        {
            var page = await query.ReadNextAsync(cancellationToken);
            results.AddRange(page);
        }

        return results.AsReadOnly();
    }

    public async Task<bool> ExistsAsync(string projectId, string jiraIssueId, CancellationToken cancellationToken = default)
    {
        var query = _container.GetItemLinqQueryable<QueuedRun>(requestOptions: new QueryRequestOptions
        {
            PartitionKey = new PartitionKey(projectId)
        })
        .Where(r => r.ProjectId == projectId && r.JiraIssueId == jiraIssueId)
        .CountAsync(cancellationToken);

        var count = await query;
        return count.Resource > 0;
    }

    public async Task<QueuedRun> EnqueueAsync(QueuedRun queuedRun, CancellationToken cancellationToken = default)
    {
        var response = await _container.CreateItemAsync(queuedRun, new PartitionKey(queuedRun.ProjectId), cancellationToken: cancellationToken);
        return response.Resource;
    }

    public async Task<QueuedRun?> DequeueNextAsync(string projectId, CancellationToken cancellationToken = default)
    {
        var queue = await GetQueueAsync(projectId, cancellationToken);
        return queue.FirstOrDefault();
    }

    public async Task DeleteAsync(string projectId, string id, CancellationToken cancellationToken = default)
    {
        await _container.DeleteItemAsync<QueuedRun>(id, new PartitionKey(projectId), cancellationToken: cancellationToken);
    }
}
