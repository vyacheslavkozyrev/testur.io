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

    public async Task<IReadOnlyList<StepResult>> GetByRunAsync(
        string projectId,
        string testRunId,
        CancellationToken cancellationToken = default)
    {
        var results = new List<StepResult>();
        var query = _container.GetItemLinqQueryable<StepResult>(requestOptions: new QueryRequestOptions
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

    public async Task<StepResult> CreateAsync(StepResult stepResult, CancellationToken cancellationToken = default)
    {
        var response = await _container.CreateItemAsync(stepResult, new PartitionKey(stepResult.ProjectId), cancellationToken: cancellationToken);
        return response.Resource;
    }
}
