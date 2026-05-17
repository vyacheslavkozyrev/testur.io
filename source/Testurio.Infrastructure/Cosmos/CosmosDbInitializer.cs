using Microsoft.Azure.Cosmos;

namespace Testurio.Infrastructure.Cosmos;

/// <summary>
/// Creates the Cosmos DB database and all containers at startup when they do not already exist.
/// Safe to call against the production account — CreateIfNotExistsAsync is a no-op when the
/// resource already exists. Primarily useful against the local emulator during development.
/// </summary>
public sealed class CosmosDbInitializer(CosmosClient cosmosClient, string databaseName)
{
    private static readonly (string Name, string PartitionKeyPath)[] Containers =
    [
        ("Projects",        "/userId"),
        ("TestRuns",        "/projectId"),
        ("RunQueue",        "/projectId"),
        ("TestScenarios",   "/projectId"),
        ("StepResults",     "/projectId"),
        ("ExecutionLogs",   "/projectId"),
        ("TestResults",     "/projectId"),
        ("TestMemory",      "/userId"),
        ("PromptTemplates", "/templateType"),
    ];

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var dbResponse = await cosmosClient.CreateDatabaseIfNotExistsAsync(
            databaseName, cancellationToken: cancellationToken);

        var database = dbResponse.Database;

        foreach (var (name, partitionKeyPath) in Containers)
        {
            await database.CreateContainerIfNotExistsAsync(
                new ContainerProperties(name, partitionKeyPath),
                cancellationToken: cancellationToken);
        }
    }
}
