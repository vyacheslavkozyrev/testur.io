using Microsoft.Extensions.Logging;

namespace Testurio.Worker;

public partial class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;

    public Worker(ILogger<Worker> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            LogRunning(_logger, DateTimeOffset.UtcNow);
            await Task.Delay(1000, stoppingToken);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Worker running at: {Time}")]
    private static partial void LogRunning(ILogger logger, DateTimeOffset time);
}
