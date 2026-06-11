using System.ComponentModel.DataAnnotations;
using System.Net.Security;
using Azure.Messaging.ServiceBus;
using Azure.Storage.Blobs;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Testurio.Core.Interfaces;
using Testurio.Core.Repositories;
using Testurio.Infrastructure.Anthropic;
using Testurio.Infrastructure.Blob;
using Testurio.Infrastructure.Extensions;
using Testurio.Infrastructure.Quota;
using Testurio.Infrastructure.Cosmos;
using Testurio.Infrastructure.Embedding;
using Testurio.Infrastructure.Enforcement;
using Testurio.Infrastructure.Jira;
using Testurio.Infrastructure.Options;
using Testurio.Infrastructure.Prompt;
using Testurio.Infrastructure.ServiceBus;
using Testurio.Infrastructure.KeyVault;
using Testurio.Infrastructure.Seeding;
using Testurio.Infrastructure.Sse;
using Testurio.Infrastructure.Storage;
using Testurio.Infrastructure.Stripe;

namespace Testurio.Infrastructure;

public class InfrastructureOptions
{
    [Required] public required string CosmosDatabaseName { get; init; }
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
    [Required] public required string ExecutionLogsBlobContainerName { get; init; }
    [Required] public required string ReportTemplatesBlobContainerName { get; init; }
    [Required] public required string ReportsBlobContainerName { get; init; }
}

/// <summary>
/// Options for the Anthropic Claude API client. Validated at startup.
/// Shared by Testurio.Worker and any pipeline project that needs LLM access.
/// The API key is sourced separately via <c>AnthropicSecrets</c> from Key Vault.
/// </summary>
public class AnthropicOptions
{
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
            var secrets = sp.GetRequiredService<InfrastructureSecrets>();
            var clientOptions = new CosmosClientOptions
            {
                UseSystemTextJsonSerializerWithOptions = new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
                    NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString,
                    Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
                }
            };

            // The local Cosmos emulator uses a self-signed certificate; bypass validation so the
            // SDK does not hang on TLS handshake in development.
            if (secrets.CosmosConnectionString.Contains("localhost", StringComparison.OrdinalIgnoreCase) ||
                secrets.CosmosConnectionString.Contains("host.docker.internal", StringComparison.OrdinalIgnoreCase) ||
                secrets.CosmosConnectionString.Contains("cosmos:8081", StringComparison.OrdinalIgnoreCase))
            {
                clientOptions.HttpClientFactory = () => new HttpClient(
                    new SocketsHttpHandler
                    {
                        SslOptions = new SslClientAuthenticationOptions
                        {
                            RemoteCertificateValidationCallback = (_, _, _, _) => true
                        }
                    });
                clientOptions.ConnectionMode = ConnectionMode.Gateway;
            }

            return new CosmosClient(secrets.CosmosConnectionString, clientOptions);
        });

        services.AddSingleton(sp =>
        {
            var secrets = sp.GetRequiredService<InfrastructureSecrets>();
            return new ServiceBusClient(secrets.ServiceBusConnectionString);
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
            var secrets = sp.GetRequiredService<InfrastructureSecrets>();
            return new BlobServiceClient(secrets.BlobStorageConnectionString);
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
            var subscriptionRepo = sp.GetRequiredService<IUserSubscriptionRepository>();
            var planRepo = sp.GetRequiredService<IPlanRepository>();
            return new StatsRepository(cosmos, opts.CosmosDatabaseName, subscriptionRepo, planRepo);
        });

        // Feature 0028 (extended in 0047): prompt template repository for all pipeline stages.
        services.AddSingleton<IPromptTemplateRepository>(sp =>
        {
            var cosmos = sp.GetRequiredService<CosmosClient>();
            var opts = sp.GetRequiredService<IOptions<InfrastructureOptions>>().Value;
            return new PromptTemplateRepository(cosmos, opts.CosmosDatabaseName);
        });

        // Feature 0047: caching wrapper around IPromptTemplateRepository.
        // Registered as Singleton — HybridCache is thread-safe and the repository is also Singleton.
        services.AddHybridCache();
        services.AddSingleton<IPromptTemplateService, PromptTemplateService>();

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

        // Feature 0046: plan enforcement service.
        // Registered as Singleton — all dependencies (repositories) are also Singleton.
        // The service is stateless; per-call state lives entirely in local variables.
        services.AddSingleton<IPlanEnforcementService>(sp =>
            new PlanEnforcementService(
                sp.GetRequiredService<IUserSubscriptionRepository>(),
                sp.GetRequiredService<IPlanRepository>(),
                sp.GetRequiredService<IProjectRepository>(),
                sp.GetRequiredService<ITestRunRepository>()));

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

        // Feature 0021: quota policy — singleton because limits are static configuration.
        services.AddSingleton<IQuotaPolicy>(sp =>
            new QuotaPolicy(sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<QuotaPolicy>>()));

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
    /// Requires <c>AzureOpenAI:Endpoint</c> and <c>AzureOpenAI:EmbeddingDeployment</c> in configuration.
    /// The API key is sourced separately via <c>AzureOpenAISecrets</c> from Key Vault.
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
            var secrets = sp.GetRequiredService<AnthropicSecrets>();
            if (!string.IsNullOrEmpty(secrets.ApiKey))
                client.DefaultRequestHeaders.Add("x-api-key", secrets.ApiKey);
        })
        .AddTypedClient<ILlmGenerationClient>((client, sp) =>
        {
            var opts = sp.GetRequiredService<IOptions<AnthropicOptions>>().Value;
            var logger = sp.GetRequiredService<ILogger<AnthropicGenerationClient>>();
            return new AnthropicGenerationClient(client, opts.ModelId, logger);
        });

        return services;
    }

    /// <summary>
    /// Registers Stripe options (validated at startup) and <see cref="IStripeService"/>.
    /// Call this only from hosts that handle billing — <c>Testurio.Api</c>.
    /// Not required by <c>Testurio.Worker</c>.
    /// </summary>
    public static IServiceCollection AddStripe(this IServiceCollection services)
    {
        services.AddOptions<StripeOptions>()
            .BindConfiguration("Stripe")
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<IStripeService, StripeService>();

        return services;
    }

    // ─── T012: Key Vault Secret Loader ────────────────────────────────────────

    /// <summary>
    /// Registers the correct <see cref="IKeyVaultSecretLoader"/> implementation based on the
    /// hosting environment:
    /// <list type="bullet">
    ///   <item>Development / Test: <see cref="NullKeyVaultSecretLoader"/> — returns empty string so callers fall back to local config.</item>
    ///   <item>Develop / Production: <see cref="KeyVaultSecretLoader"/> — reads from Azure Key Vault via Managed Identity.</item>
    /// </list>
    /// Requires <c>KeyVault:Uri</c> in non-local environments.
    /// </summary>
    public static IServiceCollection AddKeyVaultSecretLoader(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        if (environment.IsTest())
        {
            services.AddSingleton<IKeyVaultSecretLoader, NullKeyVaultSecretLoader>();
        }
        else
        {
            var keyVaultUri = configuration["KeyVault:Uri"]
                ?? throw new InvalidOperationException("KeyVault:Uri is required in all environments except Test.");

            services.AddSingleton<IKeyVaultSecretLoader>(sp =>
                new KeyVaultSecretLoader(keyVaultUri, sp.GetRequiredService<ILogger<KeyVaultSecretLoader>>()));
        }

        return services;
    }

    // ─── T013: Infrastructure Secrets ─────────────────────────────────────────

    /// <summary>
    /// Resolves and registers <see cref="InfrastructureSecrets"/> as a singleton.
    /// In production, reads from Key Vault. In development, reads from local configuration.
    /// Must be called after <see cref="AddKeyVaultSecretLoader"/> and before <see cref="AddInfrastructure"/>.
    /// </summary>
    public static async Task AddInfrastructureSecretsAsync(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment,
        CancellationToken ct = default)
    {
        InfrastructureSecrets secrets;

        if (environment.IsTest())
        {
            secrets = new InfrastructureSecrets
            {
                CosmosConnectionString = configuration["Infrastructure:CosmosConnectionString"] ?? string.Empty,
                ServiceBusConnectionString = configuration["Infrastructure:ServiceBusConnectionString"] ?? string.Empty,
                BlobStorageConnectionString = configuration["Infrastructure:BlobStorageConnectionString"] ?? string.Empty,
            };
        }
        else
        {
            // Resolve via the already-registered IKeyVaultSecretLoader.
            using var sp = services.BuildServiceProvider();
            var loader = sp.GetRequiredService<IKeyVaultSecretLoader>();

            secrets = new InfrastructureSecrets
            {
                CosmosConnectionString = await loader.GetSecretAsync("cosmos-connection-string", ct),
                ServiceBusConnectionString = await loader.GetSecretAsync("servicebus-connection-string", ct),
                BlobStorageConnectionString = await loader.GetSecretAsync("blob-storage-connection-string", ct),
            };
        }

        services.AddSingleton(secrets);
    }

    // ─── T014: Anthropic Secrets ───────────────────────────────────────────────

    /// <summary>
    /// Resolves and registers <see cref="AnthropicSecrets"/> as a singleton.
    /// In production, reads from Key Vault. In development, reads from local configuration.
    /// Must be called after <see cref="AddKeyVaultSecretLoader"/> and before <see cref="AddAnthropicClient"/>.
    /// </summary>
    public static async Task AddAnthropicSecretsAsync(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment,
        CancellationToken ct = default)
    {
        AnthropicSecrets secrets;

        if (environment.IsTest())
        {
            secrets = new AnthropicSecrets
            {
                ApiKey = configuration["Claude:ApiKey"] ?? string.Empty,
            };
        }
        else
        {
            using var sp = services.BuildServiceProvider();
            var loader = sp.GetRequiredService<IKeyVaultSecretLoader>();

            secrets = new AnthropicSecrets
            {
                ApiKey = await loader.GetSecretAsync("anthropic-api-key", ct),
            };
        }

        services.AddSingleton(secrets);
    }

    // ─── T015: Azure OpenAI Secrets ───────────────────────────────────────────

    /// <summary>
    /// Resolves and registers <see cref="AzureOpenAISecrets"/> as a singleton.
    /// In production, reads from Key Vault. In development, reads from local configuration.
    /// Must be called after <see cref="AddKeyVaultSecretLoader"/> and before <see cref="AddAzureOpenAI"/>.
    /// </summary>
    public static async Task AddAzureOpenAISecretsAsync(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment,
        CancellationToken ct = default)
    {
        AzureOpenAISecrets secrets;

        if (environment.IsTest())
        {
            secrets = new AzureOpenAISecrets
            {
                ApiKey = configuration["AzureOpenAI:ApiKey"] ?? string.Empty,
            };
        }
        else
        {
            using var sp = services.BuildServiceProvider();
            var loader = sp.GetRequiredService<IKeyVaultSecretLoader>();

            secrets = new AzureOpenAISecrets
            {
                ApiKey = await loader.GetSecretAsync("azure-openai-api-key", ct),
            };
        }

        services.AddSingleton(secrets);
    }

    // ─── T016: Stripe Secrets ─────────────────────────────────────────────────

    /// <summary>
    /// Resolves and registers <see cref="StripeSecrets"/> as a singleton.
    /// In production, reads from Key Vault. In development, reads from local configuration.
    /// Must be called after <see cref="AddKeyVaultSecretLoader"/> and before <see cref="AddStripe"/>.
    /// Only required by <c>Testurio.Api</c> — not needed in <c>Testurio.Worker</c>.
    /// </summary>
    public static async Task AddStripeSecretsAsync(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment,
        CancellationToken ct = default)
    {
        StripeSecrets secrets;

        if (environment.IsTest())
        {
            secrets = new StripeSecrets
            {
                SecretKey = configuration["Stripe:SecretKey"] ?? string.Empty,
                WebhookSecret = configuration["Stripe:WebhookSecret"] ?? string.Empty,
            };
        }
        else
        {
            using var sp = services.BuildServiceProvider();
            var loader = sp.GetRequiredService<IKeyVaultSecretLoader>();

            secrets = new StripeSecrets
            {
                SecretKey = await loader.GetSecretAsync("stripe-secret-key", ct),
                WebhookSecret = await loader.GetSecretAsync("stripe-webhook-secret", ct),
            };
        }

        services.AddSingleton(secrets);
    }
}
