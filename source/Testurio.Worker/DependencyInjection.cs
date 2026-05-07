using System.ComponentModel.DataAnnotations;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Testurio.Core.Interfaces;
using Testurio.Core.Repositories;
using Testurio.Infrastructure.KeyVault;
using Testurio.Plugins.StoryParserPlugin;
using Testurio.Plugins.TestExecutorPlugin;
using Testurio.Plugins.TestGeneratorPlugin;
using Testurio.Worker.Processors;
using Testurio.Worker.Services;
using Testurio.Worker.Steps;

namespace Testurio.Worker;

public class WorkerOptions
{
    [Required]
    public required string TestRunJobQueueName { get; init; }
}

public class ClaudeOptions
{
    [Required] public required string ApiKey { get; init; }
    [Required] public required string ModelId { get; init; }
    [Required] public required string Endpoint { get; init; }
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

        // Register Semantic Kernel with the Anthropic Claude endpoint via the OpenAI-compatible connector.
        // Swap Endpoint + ModelId in config to point at vLLM for MVP.
        services.AddTransient(sp =>
        {
            var claudeOpts = sp.GetRequiredService<IOptions<ClaudeOptions>>().Value;
            var builder = Kernel.CreateBuilder();
            builder.AddOpenAIChatCompletion(
                modelId: claudeOpts.ModelId,
                endpoint: new Uri(claudeOpts.Endpoint),
                apiKey: claudeOpts.ApiKey);
            return builder.Build();
        });

        // Plugin registrations — Transient because Kernel is Transient.
        services.AddTransient<StoryParserPlugin>();
        services.AddTransient<TestGeneratorPlugin>(sp =>
        {
            var kernel = sp.GetRequiredService<Kernel>();
            var chatCompletion = kernel.GetRequiredService<IChatCompletionService>();
            var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<TestGeneratorPlugin>>();
            return new TestGeneratorPlugin(chatCompletion, logger);
        });

        // TestExecutorPlugin needs an HttpClient — register via named HttpClient factory.
        // ResponseSchemaValidator is stateless and can be Singleton.
        services.AddSingleton<ResponseSchemaValidator>();
        services.AddHttpClient<TestExecutorPlugin>()
            .ConfigureHttpClient(client =>
            {
                // Default timeout is overridden per-request inside the plugin (10-second per-step timeout).
                // Set an outer safety timeout of 5 minutes to guard against edge cases.
                client.Timeout = TimeSpan.FromMinutes(5);
            });

        // ApiTestExecutionStep is Transient: depends on Transient TestExecutorPlugin (scoped to the HttpClient factory).
        services.AddTransient<ApiTestExecutionStep>(sp => new ApiTestExecutionStep(
            sp.GetRequiredService<TestExecutorPlugin>(),
            sp.GetRequiredService<KeyVaultCredentialClient>(),
            sp.GetRequiredService<IStepResultRepository>(),
            sp.GetRequiredService<ITestRunRepository>(),
            sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<ApiTestExecutionStep>>()));

        // ScenarioGenerationStep is Transient: depends on Transient plugins.
        services.AddTransient<ScenarioGenerationStep>(sp => new ScenarioGenerationStep(
            sp.GetRequiredService<IJiraStoryClient>(),
            sp.GetRequiredService<ISecretResolver>(),
            sp.GetRequiredService<StoryParserPlugin>(),
            sp.GetRequiredService<TestGeneratorPlugin>(),
            sp.GetRequiredService<ITestScenarioRepository>(),
            sp.GetRequiredService<ITestRunRepository>(),
            sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<ScenarioGenerationStep>>()));

        // Singleton: all dependencies (ITestRunRepository, IRunQueueRepository, ITestRunJobSender) are also Singleton.
        services.AddSingleton<RunQueueManager>();

        services.AddSingleton(sp =>
        {
            var opts = sp.GetRequiredService<IOptions<WorkerOptions>>().Value;
            var sbClient = sp.GetRequiredService<ServiceBusClient>();
            var testRunRepo = sp.GetRequiredService<ITestRunRepository>();
            var projectRepo = sp.GetRequiredService<IProjectRepository>();
            // Pass IServiceProvider so the processor can resolve ScenarioGenerationStep (Transient)
            // per-message, avoiding a captive-dependency bug where a Transient is frozen inside a Singleton.
            var queueManager = sp.GetRequiredService<RunQueueManager>();
            var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<TestRunJobProcessor>>();
            return new TestRunJobProcessor(sbClient, opts.TestRunJobQueueName, testRunRepo, projectRepo, sp, queueManager, logger);
        });

        services.AddHostedService<WorkerBackgroundService>();

        return services;
    }
}
