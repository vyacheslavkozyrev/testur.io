using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;
using Testurio.Core.Interfaces;
using Testurio.Core.Models;

namespace Testurio.Infrastructure.ServiceBus;

public partial class TestRunJobSender : ITestRunJobSender
{
    private readonly ServiceBusSender _sender;
    private readonly ILogger<TestRunJobSender> _logger;

    public TestRunJobSender(ServiceBusClient serviceBusClient, string queueName, ILogger<TestRunJobSender> logger)
    {
        _sender = serviceBusClient.CreateSender(queueName);
        _logger = logger;
    }

    public virtual async Task SendAsync(TestRunJobMessage message, CancellationToken cancellationToken = default)
    {
        var body = JsonSerializer.Serialize(message);
        var sbMessage = new ServiceBusMessage(body)
        {
            MessageId = message.TestRunId,
            SessionId = message.ProjectId
        };

        await _sender.SendMessageAsync(sbMessage, cancellationToken);
        LogJobSent(_logger, message.TestRunId, message.ProjectId);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Enqueued test run job {TestRunId} for project {ProjectId}")]
    private static partial void LogJobSent(ILogger logger, string testRunId, string projectId);
}
