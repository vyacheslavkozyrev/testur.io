using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Testurio.Core.Events;
using Testurio.Core.Interfaces;
using Testurio.Core.Models;

namespace Testurio.Api.Services;

/// <summary>
/// Wire contract for the run-status-changed Service Bus message published by the Worker
/// after a test run's status is updated.
/// Fields verified against the Worker's message schema (feature 0001):
/// <list type="bullet">
///   <item><see cref="UserId"/> — B2C OID of the project owner.</item>
///   <item><see cref="ProjectId"/> — UUID of the affected project.</item>
///   <item><see cref="RunId"/> — UUID of the completed/failed run.</item>
///   <item><see cref="Status"/> — resolved <see cref="RunStatus"/> value.</item>
///   <item><see cref="StartedAt"/> — ISO 8601 timestamp when the run started.</item>
///   <item><see cref="CompletedAt"/> — ISO 8601 timestamp when the run finished (null while running).</item>
///   <item><see cref="QuotaUsage"/> — optional; included when the run triggered a quota increment.</item>
/// </list>
/// </summary>
public sealed record RunStatusChangedMessage(
    string UserId,
    string ProjectId,
    string RunId,
    RunStatus Status,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    QuotaUsage? QuotaUsage = null);

/// <summary>
/// Hosted service that subscribes to the Service Bus run-status-changed topic,
/// deserialises each <see cref="RunStatusChangedMessage"/> and fans it out to the
/// appropriate per-user SSE channel via <see cref="IDashboardStreamManager.PublishAsync"/>.
/// </summary>
public sealed partial class DashboardEventRelay : IHostedService, IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly ServiceBusProcessor _processor;
    private readonly IDashboardStreamManager _streamManager;
    private readonly ILogger<DashboardEventRelay> _logger;

    public DashboardEventRelay(
        ServiceBusClient serviceBusClient,
        string runStatusTopicName,
        IDashboardStreamManager streamManager,
        ILogger<DashboardEventRelay> logger)
    {
        _streamManager = streamManager;
        _logger = logger;

        _processor = serviceBusClient.CreateProcessor(runStatusTopicName, new ServiceBusProcessorOptions
        {
            AutoCompleteMessages = false,
            MaxConcurrentCalls = 4,
        });

        _processor.ProcessMessageAsync += OnMessageAsync;
        _processor.ProcessErrorAsync += OnErrorAsync;
    }

    public Task StartAsync(CancellationToken cancellationToken) =>
        _processor.StartProcessingAsync(cancellationToken);

    public async Task StopAsync(CancellationToken cancellationToken) =>
        await _processor.StopProcessingAsync(cancellationToken);

    public async ValueTask DisposeAsync() =>
        await _processor.DisposeAsync();

    private async Task OnMessageAsync(ProcessMessageEventArgs args)
    {
        RunStatusChangedMessage? message;
        try
        {
            message = JsonSerializer.Deserialize<RunStatusChangedMessage>(
                args.Message.Body.ToString(),
                JsonOptions);
        }
        catch (JsonException ex)
        {
            LogInvalidPayload(_logger, ex);
            await args.DeadLetterMessageAsync(args.Message, "InvalidPayload", ex.Message, CancellationToken.None);
            return;
        }

        if (message is null)
        {
            await args.DeadLetterMessageAsync(args.Message, "NullPayload", "Deserialized message was null", CancellationToken.None);
            return;
        }

        var @event = new DashboardUpdatedEvent(
            message.ProjectId,
            new LatestRunSummary(message.RunId, message.Status, message.StartedAt, message.CompletedAt),
            message.QuotaUsage);

        await _streamManager.PublishAsync(message.UserId, @event, args.CancellationToken);
        await args.CompleteMessageAsync(args.Message, args.CancellationToken);

        LogRelayed(_logger, message.UserId, message.ProjectId, message.RunId);
    }

    private Task OnErrorAsync(ProcessErrorEventArgs args)
    {
        LogServiceBusError(_logger, args.EntityPath, args.Exception);
        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "DashboardEventRelay received an invalid JSON payload")]
    private static partial void LogInvalidPayload(ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Relayed run-status-changed to SSE channel — user {UserId}, project {ProjectId}, run {RunId}")]
    private static partial void LogRelayed(ILogger logger, string userId, string projectId, string runId);

    [LoggerMessage(Level = LogLevel.Error, Message = "DashboardEventRelay Service Bus error on {EntityPath}")]
    private static partial void LogServiceBusError(ILogger logger, string entityPath, Exception ex);
}
