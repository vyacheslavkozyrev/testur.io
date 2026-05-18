using Microsoft.Extensions.Hosting;
using Testurio.Worker.Processors;

namespace Testurio.Worker;

/// <summary>
/// Background service that starts and stops the <see cref="CommentEventJobProcessor"/>
/// (feature 0031, AC-026).
/// Runs in parallel with <see cref="WorkerBackgroundService"/> — the two processors are
/// independent hosted services with no coupling between them.
/// </summary>
public sealed class CommentEventBackgroundService : BackgroundService
{
    private readonly CommentEventJobProcessor _processor;

    public CommentEventBackgroundService(CommentEventJobProcessor processor)
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
