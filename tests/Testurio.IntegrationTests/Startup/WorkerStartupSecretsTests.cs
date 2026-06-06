using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Testurio.Core.Interfaces;
using Testurio.Infrastructure;
using Testurio.Infrastructure.Cosmos;
using Testurio.Infrastructure.KeyVault;
using Testurio.Infrastructure.Options;
using Testurio.Infrastructure.Seeding;
using Testurio.Worker;

namespace Testurio.IntegrationTests.Startup;

/// <summary>
/// Integration test verifying that <c>Testurio.Worker</c> starts cleanly with
/// <see cref="NullKeyVaultSecretLoader"/> and local configuration values.
/// </summary>
public class WorkerStartupSecretsTests
{
    private static IHost BuildWorkerHost(Dictionary<string, string?> configValues)
    {
        var builder = Host.CreateApplicationBuilder();

        builder.Configuration.AddInMemoryCollection(configValues);

        builder.Services.AddKeyVaultSecretLoader(builder.Configuration, builder.Environment);

        // We use Task.Run to bridge the async calls. In real startup these are awaited
        // at the top level; here we use GetAwaiter().GetResult() for test simplicity.
        builder.Services.AddInfrastructureSecretsAsync(builder.Configuration, builder.Environment)
            .GetAwaiter().GetResult();
        builder.Services.AddAnthropicSecretsAsync(builder.Configuration, builder.Environment)
            .GetAwaiter().GetResult();
        builder.Services.AddAzureOpenAISecretsAsync(builder.Configuration, builder.Environment)
            .GetAwaiter().GetResult();

        builder.Services.AddInfrastructure();
        builder.Services.AddWorkerServices();

        // Replace I/O-bound singletons with no-ops so the host can start without Azure.
        builder.Services.AddSingleton<ISecretResolver, PassthroughSecretResolver>();
        builder.Services.AddSingleton<ICosmosDbInitializer, NoOpCosmosDbInitializer>();
        builder.Services.AddSingleton<IPromptTemplateSeeder, NoOpPromptTemplateSeeder>();
        builder.Services.AddSingleton<IPlanSeeder, NoOpPlanSeeder>();

        return builder.Build();
    }

    private static Dictionary<string, string?> ValidWorkerConfig() => new()
    {
        ["Infrastructure:CosmosConnectionString"] = "AccountEndpoint=https://localhost:8081/;AccountKey=AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA==",
        ["Infrastructure:CosmosDatabaseName"] = "TestDb",
        ["Infrastructure:ServiceBusConnectionString"] = "Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=dummykey==",
        ["Infrastructure:TestRunJobQueueName"] = "test-runs",
        ["Infrastructure:BlobStorageConnectionString"] = "UseDevelopmentStorage=true",
        ["Infrastructure:ExecutionLogsBlobContainerName"] = "execution-logs",
        ["Infrastructure:ReportTemplatesBlobContainerName"] = "report-templates",
        ["Infrastructure:ReportsBlobContainerName"] = "reports",
        ["Claude:ModelId"] = "claude-opus-4-7",
        ["AzureOpenAI:Endpoint"] = "https://test.openai.azure.com/",
        ["AzureOpenAI:EmbeddingDeployment"] = "text-embedding-3-small",
        ["Worker:TestRunJobQueueName"] = "test-runs",
    };

    [Fact]
    public void WorkerBuilds_WithNullKeyVaultLoaderAndLocalConfig()
    {
        // If BuildWorkerHost does not throw, DI container was constructed without errors.
        using var host = BuildWorkerHost(ValidWorkerConfig());
        Assert.NotNull(host);
    }

    [Fact]
    public void InfrastructureSecrets_AreRegistered_AndPopulatedFromLocalConfig()
    {
        using var host = BuildWorkerHost(ValidWorkerConfig());
        var secrets = host.Services.GetRequiredService<InfrastructureSecrets>();
        Assert.NotNull(secrets);
        Assert.NotEmpty(secrets.CosmosConnectionString);
        Assert.NotEmpty(secrets.ServiceBusConnectionString);
        Assert.NotEmpty(secrets.BlobStorageConnectionString);
    }

    [Fact]
    public void AnthropicSecrets_AreRegistered()
    {
        using var host = BuildWorkerHost(ValidWorkerConfig());
        var secrets = host.Services.GetRequiredService<AnthropicSecrets>();
        Assert.NotNull(secrets);
    }

    [Fact]
    public void AzureOpenAISecrets_AreRegistered()
    {
        using var host = BuildWorkerHost(ValidWorkerConfig());
        var secrets = host.Services.GetRequiredService<AzureOpenAISecrets>();
        Assert.NotNull(secrets);
    }

    [Fact]
    public void KeyVaultSecretLoader_IsNullLoader_InDevelopmentEnvironment()
    {
        using var host = BuildWorkerHost(ValidWorkerConfig());
        var loader = host.Services.GetRequiredService<IKeyVaultSecretLoader>();
        Assert.IsType<NullKeyVaultSecretLoader>(loader);
    }
}
