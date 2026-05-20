using Testurio.Infrastructure.Cosmos;
using Testurio.Infrastructure.Seeding;

namespace Testurio.IntegrationTests;

/// <summary>
/// No-op startup services used by all WebApplicationFactory-based integration tests to
/// avoid connecting to Cosmos DB, which is not available in the CI / local test environment.
/// </summary>
internal sealed class NoOpCosmosDbInitializer : ICosmosDbInitializer
{
    public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}

internal sealed class NoOpPromptTemplateSeeder : IPromptTemplateSeeder
{
    public Task SeedAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}

internal sealed class NoOpPlanSeeder : IPlanSeeder
{
    public Task SeedAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}
