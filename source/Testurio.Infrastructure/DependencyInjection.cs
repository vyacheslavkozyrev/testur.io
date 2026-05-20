using System.ComponentModel.DataAnnotations;
using Azure.Messaging.ServiceBus;
using Azure.Storage.Blobs;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Testurio.Core.Interfaces;
using Testurio.Core.Repositories;
using Testurio.Infrastructure.Anthropic;
using Testurio.Infrastructure.Blob;
using Testurio.Infrastructure.Cosmos;
using Testurio.Infrastructure.Embedding;
using Testurio.Infrastructure.Jira;
using Testurio.Infrastructure.Options;
using Testurio.Infrastructure.ServiceBus;
using Testurio.Infrastructure.KeyVault;
using Testurio.Infrastructure.Seeding;
using Testurio.Infrastructure.Sse;
using Testurio.Infrastructure.Storage;
using Testurio.Infrastructure.Stripe;

namespace Testurio.Infrastructure;

public class InfrastructureOptions
{
    [Required] public required string CosmosConnectionString { get; init; }
    [Required] public required string CosmosDatabaseName { get; init; }
    [Required] public required string ServiceBusConnectionString { get; init; }
    [Required] public required string TestRunJobQueueName { get; init; }
    /// <summary>
    /// Service Bus topic name for comment-created webhook events (feature 0031).
    /// Published by Testurio.Api webhook handlers; consumed by CommentEventJobProcessor in Testurio.Worker.
    /// Defaults to <c>testurio-comment-events</c> when absent from configuration.
    /// </summary>
    public string CommentEventTopicName { get; init; } = "testurio-comment-events";
    /// <summary>
    /// Service Bus subscription name on the comment-events topic consumed by Testurio.Worker (feature 0031).
    /// Defaults to <c>worker</c> when absent from configuration.
    /// </summary>
    public string CommentEventSubscriptionName { get; init; } = "worker";
    [Required] public required string BlobStorageConnectionString { get; init; }
    [Required] public required string ExecutionLogsBlobContainerName { get; init; }
    [Required] public required string ReportTemplatesBlobContainerName { get; init; }
    [Required] public required string ReportsBlobContainerName { get; init; }
}

/// <summary>
/// Options for the Anthropic Claude API client. Validated at startup.
/// Shared by Testurio.Worker and any pipeline project that needs LLM access.
/// </summary>
public class AnthropicOptions
{
    [Required] public required string ApiKey { get; init; }
    [Required] public required string ModelId { get; init; }
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

        services.AddSingleton<IUserRepository>(sp =>
        {
            var cosmos = sp.GetRequiredService<CosmosClient>();
            var opts = sp.GetRequiredService<IOptions<InfrastructureOptions>>().Value;
            return new UserRepository(cosmos, opts.CosmosDatabaseName);
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

        // Feature 0031: sender used by Testurio.Api comment webhook handlers.
        services.AddSingleton<ICommentEventSender>(sp =>
        {
            var client = sp.GetRequiredService<ServiceBusClient>();
            var opts = sp.GetRequiredService<IOptions<InfrastructureOptions>>().Value;
            var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<CommentEventSender>>();
            return new CommentEventSender(client, opts.CommentEventTopicName, logger);
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

        // Unkeyed IBlobStorageClient → execution-logs container (default for most pipeline stages).
        services.AddSingleton<IBlobStorageClient>(sp => sp.GetRequiredService<BlobStorageClient>());

        // Keyed IBlobStorageClient → reports container (used by ReportWriterPlugin, AC-033).
        services.AddKeyedSingleton<IBlobStorageClient>("reports", (sp, _) =>
        {
            var opts = sp.GetRequiredService<IOptions<InfrastructureOptions>>().Value;
            var serviceClient = sp.GetRequiredService<BlobServiceClient>();
            var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<BlobStorageClient>>();
            return new BlobStorageClient(serviceClient, opts.ReportsBlobContainerName, logger);
        });

        services.AddSingleton<TemplateRepository>(sp =>
        {
            var serviceClient = sp.GetRequiredService<BlobServiceClient>();
            var opts = sp.GetRequiredService<IOptions<InfrastructureOptions>>().Value;
            var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<TemplateRepository>>();
            return new TemplateRepository(serviceClient, opts.ReportTemplatesBlobContainerName, logger);
        });

        services.AddSingleton<ITemplateRepository>(sp => sp.GetRequiredService<TemplateRepository>());

        services.AddSingleton<IExecutionLogRepository>(sp =>
        {
            var cosmos = sp.GetRequiredService<CosmosClient>();
            var opts = sp.GetRequiredService<IOptions<InfrastructureOptions>>().Value;
            return new ExecutionLogRepository(cosmos, opts.CosmosDatabaseName);
        });

        services.AddSingleton<IStatsRepository>(sp =>
        {
            var cosmos = sp.GetRequiredService<CosmosClient>();
            var opts = sp.GetRequiredService<IOptions<InfrastructureOptions>>().Value;
            return new StatsRepository(cosmos, opts.CosmosDatabaseName);
        });

        // Feature 0028: prompt template repository for generator agents (stage 4).
        services.AddSingleton<IPromptTemplateRepository>(sp =>
        {
            var cosmos = sp.GetRequiredService<CosmosClient>();
            var opts = sp.GetRequiredService<IOptions<InfrastructureOptions>>().Value;
            return new PromptTemplateRepository(cosmos, opts.CosmosDatabaseName);
        });

        // Feature 0030: test result repository for ReportWriter (stage 6).
        services.AddSingleton<ITestResultRepository>(sp =>
        {
            var cosmos = sp.GetRequiredService<CosmosClient>();
            var opts = sp.GetRequiredService<IOptions<InfrastructureOptions>>().Value;
            return new TestResultRepository(cosmos, opts.CosmosDatabaseName);
        });

        // Feature 0028: seeder that writes initial PromptTemplate documents to Cosmos at startup.
        services.AddSingleton<IPromptTemplateSeeder, PromptTemplateSeeder>(sp =>
        {
            var cosmos = sp.GetRequiredService<CosmosClient>();
            var opts = sp.GetRequiredService<IOptions<InfrastructureOptions>>().Value;
            return new PromptTemplateSeeder(cosmos, opts.CosmosDatabaseName);
        });

        services.AddSingleton<IPlanRepository>(sp =>
        {
            var cosmos = sp.GetRequiredService<CosmosClient>();
            var opts = sp.GetRequiredService<IOptions<InfrastructureOptions>>().Value;
            return new PlanRepository(cosmos, opts.CosmosDatabaseName);
        });

        // Feature 0015: subscription repository and Stripe service.
        services.AddSingleton<IUserSubscriptionRepository>(sp =>
        {
            var cosmos = sp.GetRequiredService<CosmosClient>();
            var opts = sp.GetRequiredService<IOptions<InfrastructureOptions>>().Value;
            return new UserSubscriptionRepository(cosmos, opts.CosmosDatabaseName);
        });

        services.AddOptions<StripeOptions>()
            .BindConfiguration("Stripe")
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<IStripeService, StripeService>();

        services.AddSingleton<IPlanSeeder, PlanSeeder>(sp =>
        {
            var cosmos = sp.GetRequiredService<CosmosClient>();
            var opts = sp.GetRequiredService<IOptions<InfrastructureOptions>>().Value;
            return new PlanSeeder(cosmos, opts.CosmosDatabaseName);
        });

        services.AddSingleton<ICosmosDbInitializer, CosmosDbInitializer>(sp =>
        {
            var cosmos = sp.GetRequiredService<CosmosClient>();
            var opts = sp.GetRequiredService<IOptions<InfrastructureOptions>>().Value;
            return new CosmosDbInitializer(cosmos, opts.CosmosDatabaseName);
        });

        // Feature 0029: screenshot storage for PlaywrightExecutor assertion-step failures.
        services.AddSingleton<IScreenshotStorage>(sp =>
        {
            var serviceClient = sp.GetRequiredService<BlobServiceClient>();
            var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<BlobScreenshotStorage>>();
            return new BlobScreenshotStorage(serviceClient, logger);
        });

        // Feature 0043: singleton SSE fan-out manager — must outlive individual HTTP requests.
        services.AddSingleton<IDashboardStreamManager, DashboardStreamManager>();

        services.AddHttpClient<IJiraApiClient, JiraApiClient>();
        services.AddHttpClient<IJiraStoryClient, JiraStoryClient>();
        services.AddHttpClient<IADOClient, ADO.ADOClient>();
        services.AddHttpClient<IJiraClient, Jira.JiraAdditionalClient>();

        services.AddSingleton<Security.WebhookSecretGenerator>();

        services.AddSingleton<IProjectAccessCredentialProvider>(sp =>
            new KeyVault.ProjectAccessCredentialProvider(
                sp.GetRequiredService<ISecretResolver>()));

        services.AddSingleton<IApiTestAuthCredentialProvider>(sp =>
            new KeyVault.ApiTestAuthCredentialProvider(
                sp.GetRequiredService<ISecretResolver>()));

        // Feature 0024: work item status transition service.
        services.AddSingleton<IWorkItemTransitionService>(sp =>
            new WorkItemTransitionService(
                sp.GetRequiredService<IJiraClient>(),
                sp.GetRequiredService<IADOClient>(),
                sp.GetRequiredService<ISecretResolver>(),
                sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<WorkItemTransitionService>>()));

        return services;
    }

    /// <summary>
    /// Registers <see cref="IEmbeddingService"/> as <see cref="AzureOpenAIEmbeddingService"/>,
    /// <see cref="TestMemoryRepository"/>, and the <see cref="AzureOpenAIOptions"/> validated binding.
    /// Requires <c>AzureOpenAI:Endpoint</c>, <c>AzureOpenAI:ApiKey</c>, and
    /// <c>AzureOpenAI:EmbeddingDeployment</c> in configuration.
    /// </summary>
    public static IServiceCollection AddAzureOpenAI(this IServiceCollection services)
    {
        services.AddOptions<AzureOpenAIOptions>()
            .BindConfiguration("AzureOpenAI")
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<IEmbeddingService, AzureOpenAIEmbeddingService>();

        services.AddSingleton<ITestMemoryRepository>(sp =>
        {
            var cosmos = sp.GetRequiredService<CosmosClient>();
            var opts = sp.GetRequiredService<IOptions<InfrastructureOptions>>().Value;
            return new TestMemoryRepository(cosmos, opts.CosmosDatabaseName);
        });

        return services;
    }

    /// <summary>
    /// Registers a singleton <see cref="ILlmGenerationClient"/> backed by the Anthropic HTTP API.
    /// Call this from any host (Worker, pipeline projects) that requires Claude API access.
    /// Requires <c>Anthropic:ApiKey</c> and <c>Anthropic:ModelId</c> in configuration.
    /// </summary>
    public static IServiceCollection AddAnthropicClient(this IServiceCollection services)
    {
        services.AddOptions<AnthropicOptions>()
            .BindConfiguration("Anthropic")
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddHttpClient<ILlmGenerationClient, AnthropicGenerationClient>((sp, client) =>
        {
            var opts = sp.GetRequiredService<IOptions<AnthropicOptions>>().Value;
            client.DefaultRequestHeaders.Add("x-api-key", opts.ApiKey);
        })
        .AddTypedClient<ILlmGenerationClient>((client, sp) =>
        {
            var opts = sp.GetRequiredService<IOptions<AnthropicOptions>>().Value;
            var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<AnthropicGenerationClient>>();
            return new AnthropicGenerationClient(client, opts.ModelId, logger);
        });

        return services;
    }
}
