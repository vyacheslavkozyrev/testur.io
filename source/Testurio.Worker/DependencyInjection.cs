using System.ComponentModel.DataAnnotations;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Testurio.Worker.Processors;
using Testurio.Worker.Services;

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

        // Singleton: all dependencies (ITestRunRepository, IRunQueueRepository, ITestRunJobSender) are also Singleton.
        services.AddSingleton<RunQueueManager>();

        services.AddSingleton(sp =>
        {
            var opts = sp.GetRequiredService<IOptions<WorkerOptions>>().Value;
            var sbClient = sp.GetRequiredService<ServiceBusClient>();
            var testRunRepo = sp.GetRequiredService<Testurio.Core.Repositories.ITestRunRepository>();
            var queueManager = sp.GetRequiredService<RunQueueManager>();
            var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<TestRunJobProcessor>>();
            return new TestRunJobProcessor(sbClient, opts.TestRunJobQueueName, testRunRepo, queueManager, logger);
        });

        services.AddHostedService<WorkerBackgroundService>();

        return services;
    }
}
