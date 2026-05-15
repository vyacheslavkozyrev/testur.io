using System.ComponentModel.DataAnnotations;
using Azure.Messaging.ServiceBus;
using Azure.Storage.Blobs;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Testurio.Core.Interfaces;
using Testurio.Core.Repositories;
using Testurio.Infrastructure.Blob;
using Testurio.Infrastructure.Cosmos;
using Testurio.Infrastructure.Jira;
using Testurio.Infrastructure.ServiceBus;
using Testurio.Infrastructure.KeyVault;

namespace Testurio.Infrastructure;

public class InfrastructureOptions
{
    [Required] public required string CosmosConnectionString { get; init; }
    [Required] public required string CosmosDatabaseName { get; init; }
    [Required] public required string ServiceBusConnectionString { get; init; }
    [Required] public required string TestRunJobQueueName { get; init; }
    [Required] public required string BlobStorageConnectionString { get; init; }
    [Required] public required string ExecutionLogsBlobContainerName { get; init; }
}

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddOptions<InfrastructureOptions>()
            .BindConfiguration("Infrastructure")
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton(sp =>
        {
            var opts = sp.GetRequiredService<IOptions<InfrastructureOptions>>().Value;
            return new CosmosClient(opts.CosmosConnectionString, new CosmosClientOptions
            {
                UseSystemTextJsonSerializerWithOptions = new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
                    Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
                }
            });
        });

        services.AddSingleton(sp =>
        {
            var opts = sp.GetRequiredService<IOptions<InfrastructureOptions>>().Value;
            return new ServiceBusClient(opts.ServiceBusConnectionString);
        });

        services.AddSingleton<IProjectRepository>(sp =>
        {
            var cosmos = sp.GetRequiredService<CosmosClient>();
            var opts = sp.GetRequiredService<IOptions<InfrastructureOptions>>().Value;
            return new ProjectRepository(cosmos, opts.CosmosDatabaseName);
        });

        services.AddSingleton<ITestRunRepository>(sp =>
        {
            var cosmos = sp.GetRequiredService<CosmosClient>();
            var opts = sp.GetRequiredService<IOptions<InfrastructureOptions>>().Value;
            return new TestRunRepository(cosmos, opts.CosmosDatabaseName);
        });

        services.AddSingleton<IRunQueueRepository>(sp =>
        {
            var cosmos = sp.GetRequiredService<CosmosClient>();
            var opts = sp.GetRequiredService<IOptions<InfrastructureOptions>>().Value;
            return new RunQueueRepository(cosmos, opts.CosmosDatabaseName);
        });

        services.AddSingleton<ITestScenarioRepository>(sp =>
        {
            var cosmos = sp.GetRequiredService<CosmosClient>();
            var opts = sp.GetRequiredService<IOptions<InfrastructureOptions>>().Value;
            return new TestScenarioRepository(cosmos, opts.CosmosDatabaseName);
        });

        services.AddSingleton<IStepResultRepository>(sp =>
        {
            var cosmos = sp.GetRequiredService<CosmosClient>();
            var opts = sp.GetRequiredService<IOptions<InfrastructureOptions>>().Value;
            return new StepResultRepository(cosmos, opts.CosmosDatabaseName);
        });

        services.AddSingleton<ITestRunJobSender>(sp =>
        {
            var client = sp.GetRequiredService<ServiceBusClient>();
            var opts = sp.GetRequiredService<IOptions<InfrastructureOptions>>().Value;
            var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<TestRunJobSender>>();
            return new TestRunJobSender(client, opts.TestRunJobQueueName, logger);
        });

        services.AddSingleton(sp =>
        {
            var opts = sp.GetRequiredService<IOptions<InfrastructureOptions>>().Value;
            return new BlobServiceClient(opts.BlobStorageConnectionString);
        });

        services.AddSingleton<BlobStorageClient>(sp =>
        {
            var serviceClient = sp.GetRequiredService<BlobServiceClient>();
            var opts = sp.GetRequiredService<IOptions<InfrastructureOptions>>().Value;
            var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<BlobStorageClient>>();
            return new BlobStorageClient(serviceClient, opts.ExecutionLogsBlobContainerName, logger);
        });

        // Register IBlobStorageClient so pipeline stages (ReportWriterPlugin) and API services
        // can inject the blob client without depending on the concrete type.
        services.AddSingleton<IBlobStorageClient>(sp => sp.GetRequiredService<BlobStorageClient>());

        services.AddSingleton<IExecutionLogRepository>(sp =>
        {
            var cosmos = sp.GetRequiredService<CosmosClient>();
            var opts = sp.GetRequiredService<IOptions<InfrastructureOptions>>().Value;
            return new ExecutionLogRepository(cosmos, opts.CosmosDatabaseName);
        });

        services.AddHttpClient<IJiraApiClient, JiraApiClient>();
        services.AddHttpClient<IJiraStoryClient, JiraStoryClient>();
        services.AddHttpClient<IADOClient, ADO.ADOClient>();
        services.AddHttpClient<IJiraClient, Jira.JiraAdditionalClient>();

        services.AddSingleton<Security.WebhookSecretGenerator>();

        return services;
    }
}
