using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;
using Testurio.Core.Interfaces;
using Testurio.Core.Models;

namespace Testurio.Worker.Processors;

/// <summary>
/// Dequeues <see cref="CommentWebhookEvent"/> messages from the <c>testurio-comment-events</c>
/// Service Bus topic subscription and dispatches them to <see cref="IFeedbackLoop.ProcessAsync"/>
/// (feature 0031, AC-025 / AC-028).
///
/// On success: the message is completed (settled).
/// On JSON parse failure: the message is dead-lettered immediately (invalid payload, not retryable).
/// On unhandled exception from <c>IFeedbackLoop.ProcessAsync</c>: the message is abandoned so
/// Service Bus retries it up to the configured dead-letter threshold (AC-028).
/// </summary>
public sealed partial class CommentEventJobProcessor : IAsyncDisposable
{
    private readonly ServiceBusProcessor _processor;
    private readonly IFeedbackLoop _feedbackLoop;
    private readonly ILogger<CommentEventJobProcessor> _logger;

    public CommentEventJobProcessor(
        ServiceBusClient serviceBusClient,
        string topicName,
        string subscriptionName,
        IFeedbackLoop feedbackLoop,
        ILogger<CommentEventJobProcessor> logger)
    {
        _processor = serviceBusClient.CreateProcessor(topicName, subscriptionName, new ServiceBusProcessorOptions
        {
            AutoCompleteMessages = false,
            MaxConcurrentCalls = 1
        });
        _feedbackLoop = feedbackLoop;
        _logger = logger;

        _processor.ProcessMessageAsync += OnMessageAsync;
        _processor.ProcessErrorAsync += OnErrorAsync;
    }

    public Task StartAsync(CancellationToken cancellationToken) =>
        _processor.StartProcessingAsync(cancellationToken);

    public Task StopAsync(CancellationToken cancellationToken) =>
        _processor.StopProcessingAsync(cancellationToken);

    private async Task OnMessageAsync(ProcessMessageEventArgs args)
    {
        CommentWebhookEvent? evt;
        try
        {
            evt = args.Message.Body.ToObjectFromJson<CommentWebhookEvent>();
        }
        catch (JsonException)
        {
            await args.DeadLetterMessageAsync(
                args.Message, "InvalidPayload", "Message body is not valid JSON", CancellationToken.None);
            return;
        }

        if (evt is null)
        {
            await args.DeadLetterMessageAsync(
                args.Message, "InvalidPayload", "Could not deserialize CommentWebhookEvent", CancellationToken.None);
            return;
        }

        LogProcessing(_logger, evt.ProjectId.ToString(), evt.CommentId, evt.PmTool);

        try
        {
            await _feedbackLoop.ProcessAsync(evt, args.CancellationToken);
            await args.CompleteMessageAsync(args.Message, args.CancellationToken);
            LogCompleted(_logger, evt.ProjectId.ToString(), evt.CommentId);
        }
        catch (Exception ex)
        {
            // AC-028: do not settle — abandon so Service Bus retries up to the dead-letter threshold.
            LogFailed(_logger, evt.ProjectId.ToString(), evt.CommentId, ex);
            await args.AbandonMessageAsync(args.Message, cancellationToken: CancellationToken.None);
        }
    }

    private Task OnErrorAsync(ProcessErrorEventArgs args)
    {
        LogServiceBusError(_logger, args.EntityPath, args.Exception);
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await _processor.DisposeAsync();
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "CommentEventJobProcessor: processing comment event for project {ProjectId} / comment {CommentId} from {PmTool}")]
    private static partial void LogProcessing(ILogger logger, string projectId, string commentId, string pmTool);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "CommentEventJobProcessor: completed comment event for project {ProjectId} / comment {CommentId}")]
    private static partial void LogCompleted(ILogger logger, string projectId, string commentId);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "CommentEventJobProcessor: failed to process comment event for project {ProjectId} / comment {CommentId} — message abandoned for retry")]
    private static partial void LogFailed(ILogger logger, string projectId, string commentId, Exception ex);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "CommentEventJobProcessor: Service Bus processor error on {EntityPath}")]
    private static partial void LogServiceBusError(ILogger logger, string entityPath, Exception ex);
}
