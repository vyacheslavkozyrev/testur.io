using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Testurio.Core.Entities;
using Testurio.Core.Repositories;
using Testurio.Core.Models;
using Testurio.Worker.Services;
using Testurio.Worker.Steps;

namespace Testurio.Worker.Processors;

public partial class TestRunJobProcessor : IAsyncDisposable
{
    private readonly ServiceBusProcessor _processor;
    private readonly ITestRunRepository _testRunRepository;
    private readonly IProjectRepository _projectRepository;
    // IServiceProvider is used to resolve ScenarioGenerationStep (Transient) per-message,
    // preventing a captive-dependency bug where a Transient would be frozen inside this Singleton.
    private readonly IServiceProvider _serviceProvider;
    private readonly RunQueueManager _runQueueManager;
    private readonly ILogger<TestRunJobProcessor> _logger;

    public TestRunJobProcessor(
        ServiceBusClient serviceBusClient,
        string queueName,
        ITestRunRepository testRunRepository,
        IProjectRepository projectRepository,
        IServiceProvider serviceProvider,
        RunQueueManager runQueueManager,
        ILogger<TestRunJobProcessor> logger)
    {
        _processor = serviceBusClient.CreateProcessor(queueName, new ServiceBusProcessorOptions
        {
            AutoCompleteMessages = false,
            MaxConcurrentCalls = 1
        });
        _testRunRepository = testRunRepository;
        _projectRepository = projectRepository;
        _serviceProvider = serviceProvider;
        _runQueueManager = runQueueManager;
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

            await ExecutePipelineAsync(testRun, args.CancellationToken);

            // ApiTestExecutionStep (feature 0003) sets testRun.Status to Completed or Failed.
            // Only record the completion timestamp here — do not override the pipeline-determined status.
            testRun.CompletedAt = DateTimeOffset.UtcNow;
            await _testRunRepository.UpdateAsync(testRun, args.CancellationToken);

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
                await _testRunRepository.UpdateAsync(testRun, CancellationToken.None);
            }
            catch (Exception updateEx)
            {
                LogStatusUpdateFailed(_logger, message.TestRunId, updateEx);
            }

            // ScenarioGenerationException is a permanent failure — dead-letter so Service Bus
            // does not retry and flood Claude with repeated calls for the same broken run.
            if (ex is ScenarioGenerationException)
                await args.DeadLetterMessageAsync(args.Message, ex.GetType().Name, ex.Message, CancellationToken.None);
            else
                await args.AbandonMessageAsync(args.Message, cancellationToken: CancellationToken.None);
        }
    }

    private async Task ExecutePipelineAsync(TestRun testRun, CancellationToken cancellationToken)
    {
        // Load the full project config in a single Cosmos read (architecture requirement).
        var project = await _projectRepository.GetByIdAsync(testRun.UserId, testRun.ProjectId, cancellationToken);
        if (project is null)
        {
            throw new InvalidOperationException($"Project {testRun.ProjectId} not found for user {testRun.UserId}");
        }

        // Step 1: Generate test scenarios from the Jira story (feature 0002).
        // Resolve ScenarioGenerationStep (Transient) fresh per-message to avoid capturing a stale instance.
        // ScenarioGenerationStep marks the run as Failed and throws on any error — the outer
        // catch in OnMessageAsync will then abandon the message and advance the queue.
        var scenarioStep = _serviceProvider.GetRequiredService<ScenarioGenerationStep>();
        var scenarios = await scenarioStep.ExecuteAsync(testRun, project, cancellationToken);

        // Step 2: Execute API tests against the product URL (feature 0003).
        // ApiTestExecutionStep is Transient — resolve fresh per-message.
        // It sets the run status (Completed/Failed) and persists all step results.
        var executionStep = _serviceProvider.GetRequiredService<ApiTestExecutionStep>();
        await executionStep.ExecuteAsync(testRun, project, scenarios, cancellationToken);

        // Step 3: ReportWriter wired in feature 0004.
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
