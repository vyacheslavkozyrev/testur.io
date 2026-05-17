using Microsoft.Azure.Cosmos;
using Testurio.Core.Entities;
using Testurio.Core.Repositories;

namespace Testurio.Infrastructure.Cosmos;

/// <summary>
/// Writes <see cref="TestResult"/> documents to the <c>TestResults</c> Cosmos DB container
/// partitioned by <c>userId</c>.
/// Implements <see cref="ITestResultRepository"/> for stage 6 (ReportWriter / feature 0030).
/// </summary>
public sealed class TestResultRepository : ITestResultRepository
{
    private readonly Container _container;

    public TestResultRepository(CosmosClient cosmosClient, string databaseName)
    {
        _container = cosmosClient.GetContainer(databaseName, "TestResults");
    }

    /// <inheritdoc />
    public async Task SaveAsync(TestResult result, CancellationToken ct = default)
    {
        await _container.CreateItemAsync(
            result,
            new PartitionKey(result.UserId),
            cancellationToken: ct);
    }
}
