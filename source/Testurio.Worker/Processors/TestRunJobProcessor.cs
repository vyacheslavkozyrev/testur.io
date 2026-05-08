using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;
using Testurio.Core.Entities;
using Testurio.Core.Enums;
using Testurio.Core.Repositories;
using Testurio.Core.Models;
using Testurio.Worker.Services;
using Testurio.Worker.Steps;

namespace Testurio.Worker.Processors;

public partial class TestRunJobProcessor : IAsyncDisposable
{
    private readonly ServiceBusProcessor _processor;
    private readonly ITestRunRepository _testRunRepository;
    private readonly RunQueueManager _runQueueManager;
    private readonly ReportDeliveryStep _reportDeliveryStep;
    private readonly ILogger<TestRunJobProcessor> _logger;

    public TestRunJobProcessor(
        ServiceBusClient serviceBusClient,
        string queueName,
        ITestRunRepository testRunRepository,
        RunQueueManager runQueueManager,
        ReportDeliveryStep reportDeliveryStep,
        ILogger<TestRunJobProcessor> logger)
    {
        _processor = serviceBusClient.CreateProcessor(queueName, new ServiceBusProcessorOptions
        {
            AutoCompleteMessages = false,
            MaxConcurrentCalls = 1
        });
        _testRunRepository = testRunRepository;
        _runQueueManager = runQueueManager;
        _reportDeliveryStep = reportDeliveryStep;
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
        TestRunJobMessage? message;
        try
        {
            message = args.Message.Body.ToObjectFromJson<TestRunJobMessage>();
        }
        catch (JsonException)
        {
            // Use CancellationToken.None — the host may be shutting down but dead-lettering must complete.
            await args.DeadLetterMessageAsync(args.Message, "InvalidPayload", "Message body is not valid JSON", CancellationToken.None);
            return;
        }

        if (message is null)
        {
            await args.DeadLetterMessageAsync(args.Message, "InvalidPayload", "Could not deserialize message body", CancellationToken.None);
            return;
        }

        LogProcessing(_logger, message.TestRunId, message.ProjectId);

        var testRun = await _testRunRepository.GetByIdAsync(message.ProjectId, message.TestRunId, args.CancellationToken);
        if (testRun is null)
        {
            // Dead-letter first so the message is removed regardless of whether queue dispatch succeeds.
            // Then advance the run queue; if dispatch fails the dead-letter is already committed.
            // Use CancellationToken.None — host may be shutting down but dead-lettering must complete.
            await args.DeadLetterMessageAsync(args.Message, "TestRunNotFound", $"TestRun {message.TestRunId} not found", CancellationToken.None);
            await _runQueueManager.OnRunCompletedAsync(message.ProjectId, args.CancellationToken);
            return;
        }

        try
        {
            testRun.Status = TestRunStatus.Active;
            testRun.StartedAt = DateTimeOffset.UtcNow;
            await _testRunRepository.UpdateAsync(testRun, args.CancellationToken);

            // Pipeline: StoryParser (0002) → TestGenerator (0002) → ApiTestExecution (0003) → ReportDelivery (0004)
            // ReportDeliveryStep sets the terminal run status (Completed or ReportDeliveryFailed) before returning.
            await ExecutePipelineAsync(testRun, args.CancellationToken);

            // Complete the message before dispatching the next queued run so that a failure in
            // OnRunCompletedAsync does not cause this message to be redelivered and the run queue
            // to be advanced a second time.
            await args.CompleteMessageAsync(args.Message, args.CancellationToken);
            await _runQueueManager.OnRunCompletedAsync(message.ProjectId, args.CancellationToken);
            LogCompleted(_logger, message.TestRunId, message.ProjectId);
        }
        catch (Exception ex)
        {
            LogFailed(_logger, message.TestRunId, message.ProjectId, ex);
            testRun.Status = TestRunStatus.Failed;
            try
            {
                // Use CancellationToken.None — the host may be shutting down (args.CancellationToken already
                // cancelled) but the status-update write must complete to avoid a stuck Active/Pending record.
                await _testRunRepository.UpdateAsync(testRun, CancellationToken.None);
            }
            catch (Exception updateEx)
            {
                LogStatusUpdateFailed(_logger, message.TestRunId, updateEx);
            }
            await args.AbandonMessageAsync(args.Message, cancellationToken: CancellationToken.None);
        }
    }

    private async Task ExecutePipelineAsync(TestRun testRun, CancellationToken cancellationToken)
    {
        // StoryParser and TestGenerator steps (feature 0002) and ApiTestExecution step (feature 0003)
        // will be inserted here before ReportDeliveryStep when those features are implemented.
        await _reportDeliveryStep.ExecuteAsync(testRun, cancellationToken);
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

    [LoggerMessage(Level = LogLevel.Information, Message = "Processing test run job {TestRunId} for project {ProjectId}")]
    private static partial void LogProcessing(ILogger logger, string testRunId, string projectId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Completed test run {TestRunId} for project {ProjectId}")]
    private static partial void LogCompleted(ILogger logger, string testRunId, string projectId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to process test run {TestRunId} for project {ProjectId}")]
    private static partial void LogFailed(ILogger logger, string testRunId, string projectId, Exception ex);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to update status to Failed for test run {TestRunId}")]
    private static partial void LogStatusUpdateFailed(ILogger logger, string testRunId, Exception ex);

    [LoggerMessage(Level = LogLevel.Error, Message = "Service Bus processor error on {EntityPath}")]
    private static partial void LogServiceBusError(ILogger logger, string entityPath, Exception ex);
}
