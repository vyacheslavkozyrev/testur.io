using Microsoft.Extensions.Hosting;
using Testurio.Worker.Processors;

namespace Testurio.Worker;

public class WorkerBackgroundService : BackgroundService
{
    private readonly TestRunJobProcessor _processor;

    public WorkerBackgroundService(TestRunJobProcessor processor)
    {
        _processor = processor;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _processor.StartAsync(stoppingToken);
        await Task.Delay(Timeout.Infinite, stoppingToken).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        await _processor.StopAsync(CancellationToken.None);
    }
}
