using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Testurio.Core.Entities;
using Testurio.Core.Repositories;

namespace Testurio.Infrastructure.Cosmos;

public class TestScenarioRepository : ITestScenarioRepository
{
    private readonly Container _container;

    public TestScenarioRepository(CosmosClient cosmosClient, string databaseName)
    {
        _container = cosmosClient.GetContainer(databaseName, "TestScenarios");
    }

    public async Task<IReadOnlyList<TestScenario>> GetByRunAsync(
        string projectId,
        string testRunId,
        CancellationToken cancellationToken = default)
    {
        var results = new List<TestScenario>();
        var query = _container.GetItemLinqQueryable<TestScenario>(requestOptions: new QueryRequestOptions
        {
            PartitionKey = new PartitionKey(projectId)
        })
        .Where(s => s.ProjectId == projectId && s.TestRunId == testRunId)
        .ToFeedIterator();

        while (query.HasMoreResults)
        {
            var page = await query.ReadNextAsync(cancellationToken);
            results.AddRange(page);
        }

        return results.AsReadOnly();
    }

    public async Task<TestScenario> CreateAsync(TestScenario scenario, CancellationToken cancellationToken = default)
    {
        var response = await _container.CreateItemAsync(scenario, new PartitionKey(scenario.ProjectId), cancellationToken: cancellationToken);
        return response.Resource;
    }

    private const int CosmosTransactionalBatchLimit = 100;

    public async Task CreateBatchAsync(IEnumerable<TestScenario> scenarios, CancellationToken cancellationToken = default)
    {
        var list = scenarios.ToList();
        if (list.Count == 0)
            return;

        var partitionKey = new PartitionKey(list[0].ProjectId);
        for (var offset = 0; offset < list.Count; offset += CosmosTransactionalBatchLimit)
        {
            var chunk = list.Skip(offset).Take(CosmosTransactionalBatchLimit);
            var batch = _container.CreateTransactionalBatch(partitionKey);
            foreach (var scenario in chunk)
                batch.CreateItem(scenario);

            using var response = await batch.ExecuteAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException(
                    $"Cosmos transactional batch failed with status {(int)response.StatusCode}: {response.ErrorMessage}");
        }
    }
}
