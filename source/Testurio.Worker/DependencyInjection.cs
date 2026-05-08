using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Testurio.Core.Interfaces;
using Testurio.Core.Repositories;
using Testurio.Plugins.ReportWriterPlugin;
using Testurio.Worker.Processors;
using Testurio.Worker.Services;
using Testurio.Worker.Steps;

namespace Testurio.Worker;

public class WorkerOptions
{
    [System.ComponentModel.DataAnnotations.Required]
    public required string TestRunJobQueueName { get; init; }
}

public static class DependencyInjection
{
    public static IServiceCollection AddWorkerServices(this IServiceCollection services)
    {
        services.AddOptions<WorkerOptions>()
            .BindConfiguration("Worker")
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Singleton: all dependencies (ITestRunRepository, IRunQueueRepository, ITestRunJobSender) are also Singleton.
        services.AddSingleton<RunQueueManager>();

        // Report pipeline — all registered as Singleton to match repository lifetimes.
        services.AddSingleton<ReportBuilderService>();
        services.AddSingleton<ReportWriterPlugin>(sp => new ReportWriterPlugin(
            sp.GetRequiredService<ITestRunRepository>(),
            sp.GetRequiredService<ITestScenarioRepository>(),
            sp.GetRequiredService<IStepResultRepository>(),
            sp.GetRequiredService<IProjectRepository>(),
            sp.GetRequiredService<IJiraApiClient>(),
            sp.GetRequiredService<ISecretResolver>(),
            sp.GetRequiredService<ReportBuilderService>(),
            sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<ReportWriterPlugin>>()));
        services.AddSingleton<ReportDeliveryStep>();

        services.AddSingleton(sp =>
        {
            var opts = sp.GetRequiredService<IOptions<WorkerOptions>>().Value;
            var sbClient = sp.GetRequiredService<ServiceBusClient>();
            var testRunRepo = sp.GetRequiredService<ITestRunRepository>();
            var queueManager = sp.GetRequiredService<RunQueueManager>();
            var reportDeliveryStep = sp.GetRequiredService<ReportDeliveryStep>();
            var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<TestRunJobProcessor>>();
            return new TestRunJobProcessor(sbClient, opts.TestRunJobQueueName, testRunRepo, queueManager, reportDeliveryStep, logger);
        });

        services.AddHostedService<WorkerBackgroundService>();

        return services;
    }
}
