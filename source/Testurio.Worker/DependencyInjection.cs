using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Testurio.Worker.Processors;
using Testurio.Worker.Services;

namespace Testurio.Worker;

public class WorkerOptions
{
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
