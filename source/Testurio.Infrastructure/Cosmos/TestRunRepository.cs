using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Testurio.Core.Entities;
using Testurio.Core.Repositories;

namespace Testurio.Infrastructure.Cosmos;

public class TestRunRepository : ITestRunRepository
{
    private readonly Container _container;

    public TestRunRepository(CosmosClient cosmosClient, string databaseName)
    {
        _container = cosmosClient.GetContainer(databaseName, "TestRuns");
    }

    public async Task<TestRun?> GetByIdAsync(string projectId, string id, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _container.ReadItemAsync<TestRun>(id, new PartitionKey(projectId), cancellationToken: cancellationToken);
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<IReadOnlyList<TestRun>> GetByProjectAsync(string projectId, int limit = 50, CancellationToken cancellationToken = default)
    {
        var results = new List<TestRun>();
        var query = _container.GetItemLinqQueryable<TestRun>(requestOptions: new QueryRequestOptions
        {
            PartitionKey = new PartitionKey(projectId),
            MaxItemCount = limit
        })
        .Where(r => r.ProjectId == projectId)
        .OrderByDescending(r => r.CreatedAt)
        .ToFeedIterator();

        while (query.HasMoreResults)
        {
            var page = await query.ReadNextAsync(cancellationToken);
            results.AddRange(page);
            if (results.Count >= limit) break;
        }

        return results.AsReadOnly();
    }

    public async Task<TestRun> CreateAsync(TestRun testRun, CancellationToken cancellationToken = default)
    {
        var response = await _container.CreateItemAsync(testRun, new PartitionKey(testRun.ProjectId), cancellationToken: cancellationToken);
        return response.Resource;
    }

    public async Task<TestRun> UpdateAsync(TestRun testRun, CancellationToken cancellationToken = default)
    {
        var response = await _container.ReplaceItemAsync(testRun, testRun.Id, new PartitionKey(testRun.ProjectId), cancellationToken: cancellationToken);
        return response.Resource;
    }
}
