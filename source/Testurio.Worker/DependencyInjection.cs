using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Testurio.Core.Interfaces;
using Testurio.Core.Repositories;
using Testurio.Infrastructure;
using Testurio.Infrastructure.Anthropic;
using Testurio.Infrastructure.Blob;
using Testurio.Infrastructure.KeyVault;
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
    [System.ComponentModel.DataAnnotations.Required]
    public required string ApiKey { get; init; }
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

        // Anthropic Claude API client for scenario generation (legacy worker-level registration).
        // The StoryParser pipeline stage resolves ILlmGenerationClient from the same registration.
        services.AddHttpClient<ILlmGenerationClient, AnthropicGenerationClient>((sp, client) =>
        {
            var opts = sp.GetRequiredService<IOptions<ClaudeOptions>>().Value;
            client.DefaultRequestHeaders.Add("x-api-key", opts.ApiKey);
        })
        .AddTypedClient<ILlmGenerationClient>((client, sp) =>
        {
            var opts = sp.GetRequiredService<IOptions<ClaudeOptions>>().Value;
            var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<AnthropicGenerationClient>>();
            return new AnthropicGenerationClient(client, opts.ModelId, logger);
        });

        // StoryParser pipeline stage (feature 0025).
        services.AddStoryParser();

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
            var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<TestRunJobProcessor>>();
            return new TestRunJobProcessor(sbClient, opts.TestRunJobQueueName, testRunRepo, projectRepo, sp, queueManager, reportDeliveryStep, logger);
        });

        services.AddHostedService<WorkerBackgroundService>();

        return services;
    }
}
