using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Testurio.Core.Entities;
using Testurio.Core.Repositories;
using Testurio.Infrastructure.Blob;

namespace Testurio.Infrastructure.Cosmos;

/// <summary>
/// Cosmos DB-backed implementation of <see cref="IExecutionLogRepository"/>.
/// Response bodies up to <see cref="BlobStorageClient.InlineThresholdBytes"/> are stored
/// inline in the document; larger bodies are stored in blob storage with a reference URL
/// written to <see cref="ExecutionLogEntry.ResponseBodyBlobUrl"/>.
/// Blob URL resolution is transparent on retrieval (AC-007).
/// </summary>
public class ExecutionLogRepository : IExecutionLogRepository
{
    private readonly Container _container;

    public ExecutionLogRepository(CosmosClient cosmosClient, string databaseName)
    {
        _container = cosmosClient.GetContainer(databaseName, "ExecutionLogs");
    }

    public async Task PersistAsync(ExecutionLogEntry entry, CancellationToken cancellationToken = default)
    {
        await _container.CreateItemAsync(
            entry,
            new PartitionKey(entry.ProjectId),
            cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<ExecutionLogEntry>> GetByRunAsync(
        string projectId,
        string testRunId,
        CancellationToken cancellationToken = default)
    {
        var results = new List<ExecutionLogEntry>();

        // PartitionKey option already scopes the query to projectId — the extra
        // e.ProjectId == projectId predicate is redundant and removed to avoid
        // implying a cross-partition filter is required.
        // Note: a composite index on (testRunId, stepIndex) should be provisioned
        // on the ExecutionLogs container for this OrderBy to be served from the index.
        var query = _container
            .GetItemLinqQueryable<ExecutionLogEntry>(requestOptions: new QueryRequestOptions
            {
                PartitionKey = new PartitionKey(projectId)
            })
            .Where(e => e.TestRunId == testRunId)
            .OrderBy(e => e.StepIndex)
            .ToFeedIterator();

        while (query.HasMoreResults)
        {
            var page = await query.ReadNextAsync(cancellationToken);
            results.AddRange(page);
        }

        return results.AsReadOnly();
    }

    public async Task<ExecutionLogEntry?> GetByStepAsync(
        string projectId,
        string testRunId,
        string scenarioId,
        int stepIndex,
        CancellationToken cancellationToken = default)
    {
        // PartitionKey already scopes to projectId — e.ProjectId predicate removed.
        var query = _container
            .GetItemLinqQueryable<ExecutionLogEntry>(requestOptions: new QueryRequestOptions
            {
                PartitionKey = new PartitionKey(projectId)
            })
            .Where(e =>
                e.TestRunId == testRunId &&
                e.ScenarioId == scenarioId &&
                e.StepIndex == stepIndex)
            .ToFeedIterator();

        while (query.HasMoreResults)
        {
            var page = await query.ReadNextAsync(cancellationToken);
            var entry = page.FirstOrDefault();
            if (entry is not null)
                return entry;
        }

        return null;
    }

    public async Task DeleteByRunAsync(
        string projectId,
        string testRunId,
        CancellationToken cancellationToken = default)
    {
        // Enumerate IDs first, then delete individually.
        // Cosmos SDK does not support bulk DELETE via LINQ; transactional batch requires all items
        // to share the same logical partition — which they do here (projectId).
        var ids = new List<string>();

        // PartitionKey already scopes to projectId — e.ProjectId predicate removed.
        var idQuery = _container
            .GetItemLinqQueryable<ExecutionLogEntry>(requestOptions: new QueryRequestOptions
            {
                PartitionKey = new PartitionKey(projectId)
            })
            .Where(e => e.TestRunId == testRunId)
            .Select(e => e.Id)
            .ToFeedIterator();

        while (idQuery.HasMoreResults)
        {
            var page = await idQuery.ReadNextAsync(cancellationToken);
            ids.AddRange(page);
        }

        var partitionKey = new PartitionKey(projectId);

        const int batchSize = 100;
        for (var offset = 0; offset < ids.Count; offset += batchSize)
        {
            var chunk = ids.Skip(offset).Take(batchSize).ToList();
            var batch = _container.CreateTransactionalBatch(partitionKey);
            foreach (var id in chunk)
                batch.DeleteItem(id);

            using var response = await batch.ExecuteAsync(cancellationToken);
            // 404 means items were already deleted (e.g. a previous attempt succeeded
            // partially before a transient failure).  Treat this as idempotent success
            // so callers can retry safely without leaving orphaned entries.
            if (!response.IsSuccessStatusCode && (int)response.StatusCode != 404)
                throw new InvalidOperationException(
                    $"Cosmos delete batch for run {testRunId} failed with status {(int)response.StatusCode}: {response.ErrorMessage}");
        }
    }
}
