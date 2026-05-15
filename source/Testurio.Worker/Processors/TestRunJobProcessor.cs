using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Testurio.Core.Entities;
using Testurio.Core.Enums;
using Testurio.Core.Exceptions;
using Testurio.Core.Interfaces;
using Testurio.Core.Models;
using Testurio.Core.Repositories;
using Testurio.Pipeline.StoryParser;
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
    private readonly ILogger<TestRunJobProcessor> _logger;

    public TestRunJobProcessor(
        ServiceBusClient serviceBusClient,
        string queueName,
        ITestRunRepository testRunRepository,
        IProjectRepository projectRepository,
        IServiceProvider serviceProvider,
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
        _projectRepository = projectRepository;
        _serviceProvider = serviceProvider;
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

            // Full pipeline:
            //   Stage 1: StoryParser (0025) — parse work item → ParsedStory, update ParserMode
            //   Stage 2: ScenarioGeneration (0002) — generate test scenarios
            //   Stage 3: ApiTestExecution (0003) — execute API tests
            //   Stage 4: ReportDelivery (0004) — post report to PM tool
            // ReportDeliveryStep sets the terminal run status (Completed or ReportDeliveryFailed).
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

            // StoryParserException and ScenarioGenerationException are permanent failures — dead-letter
            // so Service Bus does not retry and flood the LLM with repeated calls for the same broken run.
            if (ex is StoryParserException or ScenarioGenerationException)
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

        // Stage 1: Build WorkItem from run context and parse the story (feature 0025).
        // The WorkItem carries the issue key and PM tool type so TemplateChecker can detect
        // non-conformance and PmToolCommentPoster can post the warning comment.
        //
        // KNOWN ARCHITECTURAL DEBT (tracked for feature 0002 refactor):
        // Description and AcceptanceCriteria are empty stubs at this point — the full story content
        // is currently fetched inside ScenarioGenerationStep (feature 0002). Because both fields are
        // empty, the TemplateChecker will always report non-conformant and the parser will always take
        // the AI-conversion path. Moving the story fetch from ScenarioGenerationStep to here is
        // required to make the direct-parse path functional. Until that refactor is complete,
        // ParserMode will always be recorded as AiConverted.
        var workItem = BuildWorkItem(testRun, project);

        // Resolve via IStoryParser (interface contract). StoryParserService is also resolved directly
        // for the project-aware overload that posts the PM tool warning comment. This concrete resolve
        // is intentional and documented — it will be eliminated when IStoryParser is extended to
        // accept Project? as a parameter in a follow-up task.
        var storyParser = _serviceProvider.GetRequiredService<IStoryParser>();
        try
        {
            // The ParsedStory result will be passed to Stage 2 once the story-fetch refactor
            // (feature 0002) moves content retrieval to this stage. Until then the result is
            // intentionally not forwarded — ScenarioGenerationStep still fetches story content itself.
            _ = storyParser is StoryParserService sps
                ? await sps.ParseAsync(workItem, project, cancellationToken)
                : await storyParser.ParseAsync(workItem, cancellationToken);
        }
        catch (StoryParserException)
        {
            // AC-018: record failure detail before re-throwing so the run history is updated.
            testRun.SkipReason = "Failed — StoryParser error";
            await _testRunRepository.UpdateAsync(testRun, CancellationToken.None);
            throw;
        }

        // AC-020: persist parserMode to the TestRun record after the parse step completes.
        // ParserMode is determined from whether the work item was conformant.
        // See architectural debt note above — currently always AiConverted due to empty stubs.
        var templateChecker = _serviceProvider.GetRequiredService<TemplateChecker>();
        testRun.ParserMode = templateChecker.IsConformant(workItem) ? ParserMode.Direct : ParserMode.AiConverted;
        await _testRunRepository.UpdateAsync(testRun, cancellationToken);
        LogParsed(_logger, testRun.Id, testRun.ParserMode.Value.ToString());

        // Stage 2: Generate test scenarios (feature 0002).
        var scenarioStep = _serviceProvider.GetRequiredService<ScenarioGenerationStep>();
        var scenarios = await scenarioStep.ExecuteAsync(testRun, project, cancellationToken);

        // Stage 3: Execute API tests against the product URL (feature 0003).
        var executionStep = _serviceProvider.GetRequiredService<ApiTestExecutionStep>();
        await executionStep.ExecuteAsync(testRun, project, scenarios, cancellationToken);

        // Stage 4: Build and post the report to Jira (feature 0004).
        await _reportDeliveryStep.ExecuteAsync(testRun, cancellationToken);
    }

    /// <summary>
    /// Builds a <see cref="WorkItem"/> from the test run and project context.
    /// Description and AcceptanceCriteria are empty at this point — full story content is fetched
    /// from Jira/ADO inside ScenarioGenerationStep (feature 0002). The WorkItem here is used
    /// by the StoryParser stage to determine parser mode and post PM tool warnings on non-conformance.
    /// </summary>
    private static WorkItem BuildWorkItem(TestRun testRun, Core.Entities.Project project)
    {
        var pmToolType = project.PmTool ?? PMToolType.Jira;
        return new WorkItem
        {
            Title = testRun.JiraIssueKey,
            Description = string.Empty,
            AcceptanceCriteria = string.Empty,
            PmToolType = pmToolType,
            IssueKey = testRun.JiraIssueKey,
            AdoWorkItemId = pmToolType == PMToolType.Ado && int.TryParse(testRun.JiraIssueId, out var id)
                ? id
                : null
        };
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

    [LoggerMessage(Level = LogLevel.Information, Message = "Story parsed for test run {TestRunId} — mode: {ParserMode}")]
    private static partial void LogParsed(ILogger logger, string testRunId, string parserMode);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to process test run {TestRunId} for project {ProjectId}")]
    private static partial void LogFailed(ILogger logger, string testRunId, string projectId, Exception ex);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to update status to Failed for test run {TestRunId}")]
    private static partial void LogStatusUpdateFailed(ILogger logger, string testRunId, Exception ex);

    [LoggerMessage(Level = LogLevel.Error, Message = "Service Bus processor error on {EntityPath}")]
    private static partial void LogServiceBusError(ILogger logger, string entityPath, Exception ex);
}
