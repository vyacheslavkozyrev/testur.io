using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Testurio.Core.Entities;
using Testurio.Core.Enums;
using Testurio.Core.Interfaces;
using Testurio.Core.Models;
using Testurio.Core.Repositories;
using Testurio.Worker.Services;
using Testurio.Worker.Steps;

namespace Testurio.Worker.Processors;

public partial class TestRunJobProcessor : IAsyncDisposable
{
    private readonly ServiceBusProcessor _processor;
    private readonly ITestRunRepository _testRunRepository;
    private readonly IProjectRepository _projectRepository;
    // IServiceProvider is used to resolve Transient steps per-message,
    // preventing a captive-dependency bug where a Transient would be frozen inside this Singleton.
    private readonly IServiceProvider _serviceProvider;
    private readonly RunQueueManager _runQueueManager;
    private readonly ReportDeliveryStep _reportDeliveryStep;
    private readonly IAgentRouter _agentRouter;
    private readonly ILogger<TestRunJobProcessor> _logger;

    public TestRunJobProcessor(
        ServiceBusClient serviceBusClient,
        string queueName,
        ITestRunRepository testRunRepository,
        IProjectRepository projectRepository,
        IServiceProvider serviceProvider,
        RunQueueManager runQueueManager,
        ReportDeliveryStep reportDeliveryStep,
        IAgentRouter agentRouter,
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
        _reportDeliveryStep = reportDeliveryStep;
        _agentRouter = agentRouter;
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

            // Full pipeline:
            //   Stage 1: StoryParser (0025) — parse work item → ParsedStory [pending feature 0025]
            //   Stage 2: AgentRouter (0026) — classify story → resolve test types
            //   Stage 3: ScenarioGeneration (0002) — generate test scenarios
            //   Stage 4: ApiTestExecution (0003) — execute API tests
            //   Stage 5: ReportDelivery (0004) — post report to PM tool
            await ExecutePipelineAsync(testRun, args.CancellationToken);

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
            // does not retry and flood the LLM with repeated calls for the same broken run.
            if (ex is ScenarioGenerationException)
                await args.DeadLetterMessageAsync(args.Message, ex.GetType().Name, ex.Message, CancellationToken.None);
            else
                await args.AbandonMessageAsync(args.Message, cancellationToken: CancellationToken.None);
        }
    }

    private async Task ExecutePipelineAsync(TestRun testRun, CancellationToken cancellationToken)
    {
        var project = await _projectRepository.GetByIdAsync(testRun.UserId, testRun.ProjectId, cancellationToken);
        if (project is null)
            throw new InvalidOperationException($"Project {testRun.ProjectId} not found for user {testRun.UserId}");

        // Stage 2: AgentRouter — classify the story and resolve test types (feature 0026).
        //
        // KNOWN ARCHITECTURAL DEBT (tracked for feature 0025 integration):
        // The ParsedStory here is constructed from the run's issue key as the title only.
        // Full story content (description, acceptance criteria) will be populated when the
        // StoryParser pipeline stage (feature 0025) is wired in before this stage.
        // Until that refactor is complete, Claude will classify based on the issue key alone.
        var parsedStory = BuildMinimalParsedStory(testRun);
        var routerResult = await _agentRouter.RouteAsync(parsedStory, project, testRun, cancellationToken);
        LogRouted(_logger, testRun.Id, string.Join(", ", routerResult.ResolvedTestTypes));

        // AC-009: if routing produced an empty list, the run is already marked Skipped by
        // AgentRouterService. Complete the message normally — a skipped run is not a failure.
        if (routerResult.ResolvedTestTypes.Length == 0)
        {
            LogSkipped(_logger, testRun.Id);
            return;
        }

        // Stage 3: Generate test scenarios (feature 0002).
        var scenarioStep = _serviceProvider.GetRequiredService<ScenarioGenerationStep>();
        var scenarios = await scenarioStep.ExecuteAsync(testRun, project, cancellationToken);

        // Stage 4: Execute API tests against the product URL (feature 0003).
        var executionStep = _serviceProvider.GetRequiredService<ApiTestExecutionStep>();
        await executionStep.ExecuteAsync(testRun, project, scenarios, cancellationToken);

        // Stage 5: Build and post the report to Jira (feature 0004).
        await _reportDeliveryStep.ExecuteAsync(testRun, cancellationToken);
    }

    /// <summary>
    /// Builds a minimal <see cref="ParsedStory"/> from the run context for routing classification.
    /// Description and AcceptanceCriteria are empty at this point — full story content will be
    /// populated when the StoryParser stage (feature 0025) is wired in before the AgentRouter stage.
    /// </summary>
    private static ParsedStory BuildMinimalParsedStory(TestRun testRun) =>
        new()
        {
            Title = testRun.JiraIssueKey,
            // Description and AcceptanceCriteria will be fully populated when the StoryParser
            // stage (feature 0025) is wired in before the AgentRouter stage. Until then,
            // a placeholder is used so the type contract (at least one AC) is honoured and
            // the limitation is visible in the Claude prompt itself.
            Description = "(Story content not yet available — pending StoryParser integration)",
            AcceptanceCriteria = new[] { "(Acceptance criteria not yet available — pending StoryParser integration)" }
        };

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

    [LoggerMessage(Level = LogLevel.Information, Message = "AgentRouter resolved types [{Types}] for test run {TestRunId}")]
    private static partial void LogRouted(ILogger logger, string testRunId, string types);

    [LoggerMessage(Level = LogLevel.Information, Message = "Test run {TestRunId} skipped — no applicable test type resolved")]
    private static partial void LogSkipped(ILogger logger, string testRunId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to process test run {TestRunId} for project {ProjectId}")]
    private static partial void LogFailed(ILogger logger, string testRunId, string projectId, Exception ex);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to update status to Failed for test run {TestRunId}")]
    private static partial void LogStatusUpdateFailed(ILogger logger, string testRunId, Exception ex);

    [LoggerMessage(Level = LogLevel.Error, Message = "Service Bus processor error on {EntityPath}")]
    private static partial void LogServiceBusError(ILogger logger, string entityPath, Exception ex);
}
