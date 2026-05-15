using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Testurio.Core.Entities;
using Testurio.Core.Enums;
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

    public async Task<TestRun?> GetActiveRunAsync(string projectId, CancellationToken cancellationToken = default)
    {
        var query = _container.GetItemLinqQueryable<TestRun>(requestOptions: new QueryRequestOptions
        {
            PartitionKey = new PartitionKey(projectId),
            MaxItemCount = 1
        })
        .Where(r => r.ProjectId == projectId && (r.Status == TestRunStatus.Active || r.Status == TestRunStatus.Pending))
        .ToFeedIterator();

        while (query.HasMoreResults)
        {
            var page = await query.ReadNextAsync(cancellationToken);
            var result = page.FirstOrDefault();
            if (result is not null) return result;
        }

        return null;
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
        // ReplaceItemAsync serialises the full TestRun entity, including the ParserMode field
        // added by feature 0025. Cosmos DB's schema-less model makes this additive and
        // backwards-compatible — existing run documents without the field deserialise fine.
        var response = await _container.ReplaceItemAsync(testRun, testRun.Id, new PartitionKey(testRun.ProjectId), cancellationToken: cancellationToken);
        return response.Resource;
    }
}
