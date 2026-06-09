using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Testurio.Core.Interfaces;
using Testurio.Core.Models;
using Testurio.Core.Repositories;
using Testurio.Infrastructure;
using Testurio.Infrastructure.Anthropic;
using Testurio.Infrastructure.Blob;
using Testurio.Infrastructure.KeyVault;
using Testurio.Infrastructure.Options;
using Testurio.Pipeline.AgentRouter;
using Testurio.Pipeline.Executors;
using Testurio.Pipeline.Generators;
using Testurio.Pipeline.MemoryRetrieval;
using Testurio.Pipeline.FeedbackLoop;
using Testurio.Pipeline.MemoryWriter;
using Testurio.Pipeline.ReportWriter;
using Testurio.Pipeline.StoryParser;
using Testurio.Plugins.ReportWriterPlugin;
using Testurio.Plugins.StoryParserPlugin;
using Testurio.Plugins.TestExecutorPlugin;
using Testurio.Plugins.TestGeneratorPlugin;
using Testurio.Worker.Processors;
using Testurio.Worker.Services;
using Testurio.Worker.Steps;

namespace Testurio.Worker;

public class WorkerOptions
{
    [System.ComponentModel.DataAnnotations.Required]
    public required string TestRunJobQueueName { get; init; }
}

public class ClaudeOptions
{
    [System.ComponentModel.DataAnnotations.Required]
    public required string ModelId { get; init; }
}

public static class DependencyInjection
{
    public static IServiceCollection AddWorkerServices(this IServiceCollection services)
    {
        services.AddOptions<WorkerOptions>()
            .BindConfiguration("Worker")
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<ClaudeOptions>()
            .BindConfiguration("Claude")
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Anthropic Claude API client for scenario generation.
        // ApiKey is sourced from AnthropicSecrets (populated from Key Vault at startup).
        // The StoryParser pipeline stage resolves ILlmGenerationClient from the same registration.
        services.AddHttpClient<ILlmGenerationClient, AnthropicGenerationClient>((sp, client) =>
        {
            var secrets = sp.GetRequiredService<AnthropicSecrets>();
            client.DefaultRequestHeaders.Add("x-api-key", secrets.ApiKey);
        })
        .AddTypedClient<ILlmGenerationClient>((client, sp) =>
        {
            var opts = sp.GetRequiredService<IOptions<ClaudeOptions>>().Value;
            var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<AnthropicGenerationClient>>();
            return new AnthropicGenerationClient(client, opts.ModelId, logger);
        });

        // StoryParser pipeline stage (feature 0025).
        services.AddStoryParser();

        // AgentRouter pipeline stage (feature 0026).
        services.AddAgentRouter();

        // MemoryRetrieval pipeline stage (feature 0027).
        services.AddAzureOpenAI();
        services.AddMemoryRetrieval();

        // Generator agents pipeline stage (feature 0028).
        services.AddGenerators();

        // Executor stage pipeline (feature 0029).
        // IScreenshotStorage is registered by AddInfrastructure (via BlobScreenshotStorage).
        // IProjectAccessCredentialProvider is registered by AddInfrastructure.
        // IHttpClientFactory is provided by AddHttpClient registrations above.
        services.AddExecutors();

        // ReportWriter pipeline stage (feature 0030).
        // Prerequisites: ILlmGenerationClient, IJiraApiClient, IADOClient, ISecretResolver,
        // ITestResultRepository — all registered above by AddWorkerServices/AddInfrastructure.
        services.AddReportWriter();

        // MemoryWriter pipeline stage (feature 0046 stub / full implementation in feature 0032).
        services.AddMemoryWriter();

        // Feature 0024: work item status transition step (Singleton — dependencies are all Singleton).
        services.AddSingleton<WorkItemTransitionStep>();

        // Singleton: all dependencies are also Singleton.
        services.AddSingleton<RunQueueManager>();

        // Scenario generation pipeline (feature 0002).
        services.AddSingleton<StoryParserPlugin>();
        services.AddSingleton<TestGeneratorPlugin>();
        services.AddSingleton<KeyVaultCredentialClient>();
        // ScenarioGenerationStep and ApiTestExecutionStep are Transient — resolved fresh per-message
        // to avoid a captive-dependency bug inside the Singleton TestRunJobProcessor.
        services.AddTransient<ScenarioGenerationStep>();
        services.AddTransient<ApiTestExecutionStep>();

        // HTTP client for API test execution (feature 0003).
        // Feature 0005: register LogPersistenceService and wire it into TestExecutorPlugin.
        services.AddSingleton<LogPersistenceService>();
        services.AddHttpClient<TestExecutorPlugin>()
            .AddTypedClient<TestExecutorPlugin>((httpClient, sp) => new TestExecutorPlugin(
                httpClient,
                sp.GetRequiredService<ResponseSchemaValidator>(),
                sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<TestExecutorPlugin>>(),
                sp.GetRequiredService<LogPersistenceService>()));
        services.AddSingleton<ResponseSchemaValidator>();

        // Report pipeline (feature 0004).
        services.AddSingleton<ReportBuilderService>();
        services.AddSingleton<ReportWriterPlugin>(sp => new ReportWriterPlugin(
            sp.GetRequiredService<ITestRunRepository>(),
            sp.GetRequiredService<ITestScenarioRepository>(),
            sp.GetRequiredService<IStepResultRepository>(),
            sp.GetRequiredService<IExecutionLogRepository>(),
            sp.GetRequiredService<IProjectRepository>(),
            sp.GetRequiredService<IJiraApiClient>(),
            sp.GetRequiredService<ISecretResolver>(),
            sp.GetRequiredService<ReportBuilderService>(),
            sp.GetRequiredService<ITemplateRepository>(),
            sp.GetRequiredKeyedService<IBlobStorageClient>("reports"),
            sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<ReportWriterPlugin>>()));
        services.AddSingleton<ReportDeliveryStep>();

        services.AddSingleton(sp =>
        {
            var opts = sp.GetRequiredService<IOptions<WorkerOptions>>().Value;
            var sbClient = sp.GetRequiredService<ServiceBusClient>();
            var testRunRepo = sp.GetRequiredService<ITestRunRepository>();
            var projectRepo = sp.GetRequiredService<IProjectRepository>();
            var queueManager = sp.GetRequiredService<RunQueueManager>();
            var reportDeliveryStep = sp.GetRequiredService<ReportDeliveryStep>();
            var agentRouter = sp.GetRequiredService<IAgentRouter>();
            var memoryRetrievalService = sp.GetRequiredService<IMemoryRetrievalService>();
            var promptTemplateService = sp.GetRequiredService<IPromptTemplateService>();
            var testGeneratorFactory = sp.GetRequiredService<ITestGeneratorFactory>();
            var executorRouter = sp.GetRequiredService<IExecutorRouter>();
            var reportWriter = sp.GetRequiredService<IReportWriter>();
            var memoryWriterService = sp.GetRequiredService<IMemoryWriterService>();
            // Feature 0046: IPlanEnforcementService is registered as Singleton — all its
            // dependencies (repositories) are also Singleton — so this resolve is safe here.
            var planEnforcementService = sp.GetRequiredService<IPlanEnforcementService>();
            var workItemTransitionStep = sp.GetRequiredService<WorkItemTransitionStep>();
            var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<TestRunJobProcessor>>();
            return new TestRunJobProcessor(
                sbClient, opts.TestRunJobQueueName, testRunRepo, projectRepo, sp,
                queueManager, reportDeliveryStep, agentRouter, memoryRetrievalService,
                promptTemplateService, testGeneratorFactory, executorRouter, reportWriter,
                memoryWriterService, planEnforcementService, workItemTransitionStep, logger);
        });

        services.AddHostedService<WorkerBackgroundService>();

        // Feature 0031: FeedbackLoop pipeline stage + CommentEventJobProcessor background service.
        // Prerequisites already satisfied by AddWorkerServices above:
        //   ITestRunRepository, IProjectRepository, IEmbeddingService (via AddAzureOpenAI),
        //   ITestMemoryRepository (via AddAzureOpenAI), IJiraApiClient, IADOClient, ISecretResolver.
        services.AddFeedbackLoop();

        services.AddSingleton<CommentEventJobProcessor>(sp =>
        {
            var infraOpts = sp.GetRequiredService<IOptions<InfrastructureOptions>>().Value;
            var sbClient = sp.GetRequiredService<ServiceBusClient>();
            var feedbackLoop = sp.GetRequiredService<IFeedbackLoop>();
            var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<CommentEventJobProcessor>>();
            return new CommentEventJobProcessor(
                sbClient,
                infraOpts.CommentEventTopicName,
                infraOpts.CommentEventSubscriptionName,
                feedbackLoop,
                logger);
        });

        services.AddHostedService<CommentEventBackgroundService>();

        return services;
    }
}
