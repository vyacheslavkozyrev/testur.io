using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Testurio.Core.Entities;
using Testurio.Core.Repositories;

namespace Testurio.Infrastructure.Cosmos;

public class StepResultRepository : IStepResultRepository
{
    private readonly Container _container;

    public StepResultRepository(CosmosClient cosmosClient, string databaseName)
    {
        _container = cosmosClient.GetContainer(databaseName, "StepResults");
    }

    public async Task<IReadOnlyList<StepResult>> GetByRunAsync(string projectId, string testRunId, CancellationToken cancellationToken = default)
    {
        var results = new List<StepResult>();
        var query = _container.GetItemLinqQueryable<StepResult>(requestOptions: new QueryRequestOptions
        {
            PartitionKey = new PartitionKey(projectId)
        })
        .Where(r => r.ProjectId == projectId && r.TestRunId == testRunId)
        .ToFeedIterator();

        while (query.HasMoreResults)
        {
            var page = await query.ReadNextAsync(cancellationToken);
            results.AddRange(page);
        }

        return results.AsReadOnly();
    }

    public async Task CreateBatchAsync(IEnumerable<StepResult> results, CancellationToken cancellationToken = default)
    {
        var list = results.ToList();
        if (list.Count == 0)
            return;

        // All step results in a run share the same projectId partition key — use TransactionalBatch
        // so either all are written or none are, ensuring consistent result data for the report writer.
        var partitionKey = new PartitionKey(list[0].ProjectId);
        var batch = _container.CreateTransactionalBatch(partitionKey);
        foreach (var result in list)
            batch.CreateItem(result);

        using var response = await batch.ExecuteAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(
                $"Cosmos transactional batch failed with status {(int)response.StatusCode}: {response.ErrorMessage}");
    }
}
