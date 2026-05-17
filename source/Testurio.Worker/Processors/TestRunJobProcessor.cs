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
    private readonly IAgentRouter _agentRouter;
    private readonly IMemoryRetrievalService _memoryRetrievalService;
    private readonly IPromptTemplateRepository _promptTemplateRepository;
    private readonly ITestGeneratorFactory _testGeneratorFactory;
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
        IMemoryRetrievalService memoryRetrievalService,
        IPromptTemplateRepository promptTemplateRepository,
        ITestGeneratorFactory testGeneratorFactory,
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
        _memoryRetrievalService = memoryRetrievalService;
        _promptTemplateRepository = promptTemplateRepository;
        _testGeneratorFactory = testGeneratorFactory;
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
            //   Stage 2: AgentRouter (0026) — classify story → resolve test types
            //   Stage 3: MemoryRetrieval (0027) — embed story, vector search → MemoryRetrievalResult
            //   Stage 4: ScenarioGeneration (0002) — generate test scenarios
            //   Stage 5: ApiTestExecution (0003) — execute API tests
            //   Stage 6: ReportDelivery (0004) — post report to PM tool
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

            // StoryParserException, ScenarioGenerationException, and InvalidOperationException
            // (e.g. missing PromptTemplate — AC-005) are permanent failures — dead-letter so
            // Service Bus does not retry and flood the LLM with repeated calls for the same broken run.
            if (ex is StoryParserException or ScenarioGenerationException or InvalidOperationException)
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
        ParsedStory parsedStory;
        try
        {
            parsedStory = storyParser is StoryParserService sps
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

        // Stage 2: AgentRouter — classify the story and resolve test types (feature 0026).
        var routerResult = await _agentRouter.RouteAsync(parsedStory, project, testRun, cancellationToken);
        LogRouted(_logger, testRun.Id, string.Join(", ", routerResult.ResolvedTestTypes));

        // AC-009: if routing produced an empty list, the run is already marked Skipped by
        // AgentRouterService. Complete the message normally — a skipped run is not a failure.
        if (routerResult.ResolvedTestTypes.Length == 0)
        {
            LogSkipped(_logger, testRun.Id);
            return;
        }

        // Stage 3: MemoryRetrieval (feature 0027) — embed the story and retrieve the top-3
        // semantically similar past scenarios scoped to this project. Any infrastructure failure
        // is caught by MemoryRetrievalService and returns an empty result; the pipeline continues.
        var memoryResult = await _memoryRetrievalService.RetrieveAsync(parsedStory, project, testRun.Id, cancellationToken);
        LogMemoryRetrieved(_logger, testRun.Id, memoryResult.Scenarios.Count);

        // Stage 4: Generate test scenarios (feature 0028).
        // Load prompt templates for all resolved test types. If any template is missing the run
        // fails immediately (AC-005) — no agent is invoked and no partial work is done.
        var generatorResults = await RunGeneratorStageAsync(
            testRun, project, parsedStory, memoryResult, routerResult.ResolvedTestTypes, cancellationToken);

        // Stage 5: Execute API tests against the product URL (feature 0003).
        var scenarioStep = _serviceProvider.GetRequiredService<ScenarioGenerationStep>();
        var scenarios = await scenarioStep.ExecuteAsync(testRun, project, cancellationToken);

        var executionStep = _serviceProvider.GetRequiredService<ApiTestExecutionStep>();
        await executionStep.ExecuteAsync(testRun, project, scenarios, cancellationToken);

        // Stage 6: Build and post the report to Jira (feature 0004).
        await _reportDeliveryStep.ExecuteAsync(testRun, cancellationToken);
    }

    /// <summary>
    /// Executes stage 4 of the pipeline: loads prompt templates, builds generator contexts,
    /// launches enabled agents with <see cref="Task.WhenAll"/>, catches per-agent failures,
    /// accumulates <see cref="TestRun.GenerationWarnings"/>, and persists them to Cosmos.
    /// </summary>
    private async Task<GeneratorResults> RunGeneratorStageAsync(
        TestRun testRun,
        Core.Entities.Project project,
        ParsedStory parsedStory,
        MemoryRetrievalResult memoryResult,
        TestType[] resolvedTestTypes,
        CancellationToken cancellationToken)
    {
        // AC-003 / AC-005: load templates for all resolved types. Throws InvalidOperationException
        // on missing template; this propagates to OnMessageAsync and dead-letters the message.
        var templateMap = new Dictionary<TestType, PromptTemplate>();
        foreach (var testType in resolvedTestTypes)
        {
            var templateType = testType == TestType.Api ? "api_test_generator" : "ui_e2e_test_generator";
            var template = await _promptTemplateRepository.GetAsync(templateType, cancellationToken);
            templateMap[testType] = template;
            LogTemplateLoaded(_logger, testRun.Id, templateType);
        }

        // Build a task per enabled test type. Non-enabled types are skipped — their scenario
        // lists in the merged result will remain empty (AC-026).
        var agentTasks = new List<(TestType Type, Task<GeneratorResults> Task)>();

        foreach (var testType in resolvedTestTypes)
        {
            var context = new GeneratorContext
            {
                ParsedStory = parsedStory,
                MemoryRetrievalResult = memoryResult,
                ProjectConfig = project,
                PromptTemplate = templateMap[testType],
                TestRunId = Guid.Parse(testRun.Id)
            };

            var agent = _testGeneratorFactory.Create(testType);
            agentTasks.Add((testType, agent.GenerateAsync(context, cancellationToken)));
        }

        // AC-025: launch all agents concurrently and wait for all to complete.
        // Task.WhenAll does not throw on the first failure — all tasks run to completion,
        // allowing per-task exception handling below (AC-033).
        try
        {
            await Task.WhenAll(agentTasks.Select(t => (Task)t.Task));
        }
        catch
        {
            // Individual task exceptions are handled below via await; swallow the aggregate here.
        }

        // Collect results and handle per-agent failures (AC-033, AC-034).
        var apiScenarios = new List<ApiTestScenario>();
        var uiE2eScenarios = new List<UiE2eTestScenario>();
        var warnings = new List<string>(testRun.GenerationWarnings);

        foreach (var (testType, task) in agentTasks)
        {
            try
            {
                var result = await task;
                apiScenarios.AddRange(result.ApiScenarios);
                uiE2eScenarios.AddRange(result.UiE2eScenarios);
            }
            catch (TestGeneratorException ex)
            {
                // AC-034: append warning string for the failing agent.
                var warning = $"{ex.TestType switch { TestType.Api => "api_test_generator", _ => "ui_e2e_test_generator" }}: JSON parse failed after {ex.Attempts} attempts";
                warnings.Add(warning);
                LogGeneratorFailed(_logger, testRun.Id, ex.TestType.ToString(), ex.Attempts, ex);
            }
        }

        // AC-038 / AC-039: persist accumulated warnings before invoking stage 5.
        testRun.GenerationWarnings = warnings.ToArray();
        await _testRunRepository.UpdateAsync(testRun, cancellationToken);

        return new GeneratorResults
        {
            ApiScenarios = apiScenarios.AsReadOnly(),
            UiE2eScenarios = uiE2eScenarios.AsReadOnly()
        };
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

    [LoggerMessage(Level = LogLevel.Information, Message = "AgentRouter resolved types [{Types}] for test run {TestRunId}")]
    private static partial void LogRouted(ILogger logger, string testRunId, string types);

    [LoggerMessage(Level = LogLevel.Information, Message = "MemoryRetrieval returned {Count} scenario(s) for test run {TestRunId}")]
    private static partial void LogMemoryRetrieved(ILogger logger, string testRunId, int count);

    [LoggerMessage(Level = LogLevel.Information, Message = "Test run {TestRunId} skipped — no applicable test type resolved")]
    private static partial void LogSkipped(ILogger logger, string testRunId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to process test run {TestRunId} for project {ProjectId}")]
    private static partial void LogFailed(ILogger logger, string testRunId, string projectId, Exception ex);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to update status to Failed for test run {TestRunId}")]
    private static partial void LogStatusUpdateFailed(ILogger logger, string testRunId, Exception ex);

    [LoggerMessage(Level = LogLevel.Information, Message = "Prompt template '{TemplateType}' loaded for test run {TestRunId}")]
    private static partial void LogTemplateLoaded(ILogger logger, string testRunId, string templateType);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Generator agent for type {TestType} failed after {Attempts} attempt(s) for test run {TestRunId}")]
    private static partial void LogGeneratorFailed(ILogger logger, string testRunId, string testType, int attempts, Exception ex);

    [LoggerMessage(Level = LogLevel.Error, Message = "Service Bus processor error on {EntityPath}")]
    private static partial void LogServiceBusError(ILogger logger, string entityPath, Exception ex);
}
