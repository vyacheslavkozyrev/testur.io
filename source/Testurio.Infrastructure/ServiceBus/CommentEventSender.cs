using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;
using Testurio.Core.Interfaces;
using Testurio.Core.Models;

namespace Testurio.Infrastructure.ServiceBus;

/// <summary>
/// Publishes <see cref="CommentWebhookEvent"/> messages to the <c>testurio-comment-events</c>
/// Service Bus topic (feature 0031, AC-024).
/// </summary>
public partial class CommentEventSender : ICommentEventSender
{
    private readonly ServiceBusSender _sender;
    private readonly ILogger<CommentEventSender> _logger;

    public CommentEventSender(ServiceBusClient serviceBusClient, string topicName, ILogger<CommentEventSender> logger)
    {
        _sender = serviceBusClient.CreateSender(topicName);
        _logger = logger;
    }

    public async Task SendAsync(CommentWebhookEvent evt, CancellationToken cancellationToken = default)
    {
        var body = JsonSerializer.Serialize(evt);
        var sbMessage = new ServiceBusMessage(body)
        {
            // Use projectId + commentId as the message ID for idempotent delivery detection.
            MessageId = $"{evt.ProjectId}:{evt.CommentId}"
        };

        await _sender.SendMessageAsync(sbMessage, cancellationToken);
        LogEventSent(_logger, evt.ProjectId.ToString(), evt.CommentId, evt.PmTool);
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "CommentEventSender: published {PmTool} comment event for project {ProjectId} / comment {CommentId}")]
    private static partial void LogEventSent(ILogger logger, string projectId, string commentId, string pmTool);
}
