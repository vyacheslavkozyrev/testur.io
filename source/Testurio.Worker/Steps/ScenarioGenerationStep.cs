using Microsoft.Extensions.Logging;
using Testurio.Core.Entities;
using Testurio.Core.Interfaces;
using Testurio.Core.Repositories;
using Testurio.Plugins.StoryParserPlugin;
using Testurio.Plugins.TestGeneratorPlugin;

namespace Testurio.Worker.Steps;

public partial class ScenarioGenerationStep
{
    private readonly IJiraStoryClient _jiraStoryClient;
    private readonly ISecretResolver _secretResolver;
    private readonly StoryParserPlugin _storyParser;
    private readonly TestGeneratorPlugin _testGenerator;
    private readonly ITestScenarioRepository _scenarioRepository;
    private readonly ITestRunRepository _testRunRepository;
    private readonly ILogger<ScenarioGenerationStep> _logger;

    public ScenarioGenerationStep(
        IJiraStoryClient jiraStoryClient,
        ISecretResolver secretResolver,
        StoryParserPlugin storyParser,
        TestGeneratorPlugin testGenerator,
        ITestScenarioRepository scenarioRepository,
        ITestRunRepository testRunRepository,
        ILogger<ScenarioGenerationStep> logger)
    {
        _jiraStoryClient = jiraStoryClient;
        _secretResolver = secretResolver;
        _storyParser = storyParser;
        _testGenerator = testGenerator;
        _scenarioRepository = scenarioRepository;
        _testRunRepository = testRunRepository;
        _logger = logger;
    }

    /// <summary>
    /// Executes the scenario generation step for a test run.
    /// Returns the generated scenarios on success; throws <see cref="ScenarioGenerationException"/> on failure.
    /// </summary>
    public async Task<IReadOnlyList<TestScenario>> ExecuteAsync(
        TestRun testRun,
        Core.Entities.Project project,
        CancellationToken cancellationToken = default)
    {
        LogStarting(_logger, testRun.Id, testRun.JiraIssueKey);

        // Resolve the Jira API token from Key Vault.
        var apiToken = await _secretResolver.ResolveAsync(project.JiraApiTokenSecretRef, cancellationToken);

        // Fetch the story content from Jira.
        var storyContent = await _jiraStoryClient.GetStoryContentAsync(
            project.JiraBaseUrl,
            testRun.JiraIssueKey,
            project.JiraEmail,
            apiToken,
            cancellationToken);

        if (storyContent is null)
        {
            await FailRunAsync(testRun, "Failed to retrieve story content from Jira", cancellationToken);
            throw new ScenarioGenerationException(testRun.Id, "Failed to retrieve story content from Jira");
        }

        // Parse the story into a formatted prompt input.
        var promptInput = _storyParser.FormatPromptInput(storyContent.Description, storyContent.AcceptanceCriteria);

        // Generate test scenarios via Claude.
        IReadOnlyList<TestScenario> scenarios;
        try
        {
            scenarios = await _testGenerator.GenerateAsync(testRun.Id, testRun.ProjectId, promptInput, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await FailRunAsync(testRun, $"Claude API error: {ex.Message}", cancellationToken);
            throw new ScenarioGenerationException(testRun.Id, $"Claude API error: {ex.Message}", ex);
        }

        // AC-006: empty response is a failure.
        if (scenarios.Count == 0)
        {
            await FailRunAsync(testRun, "Claude returned an empty scenario list", cancellationToken);
            throw new ScenarioGenerationException(testRun.Id, "Claude returned an empty scenario list");
        }

        // AC-007/008: persist all generated scenarios before proceeding.
        await _scenarioRepository.CreateBatchAsync(scenarios, cancellationToken);
        LogPersisted(_logger, scenarios.Count, testRun.Id);

        return scenarios;
    }

    private async Task FailRunAsync(TestRun testRun, string errorDetail, CancellationToken cancellationToken)
    {
        testRun.Status = TestRunStatus.Failed;
        testRun.SkipReason = $"Failed — scenario generation error: {errorDetail}";
        try
        {
            // Use CancellationToken.None so the status write completes even if the pipeline token is cancelled.
            await _testRunRepository.UpdateAsync(testRun, CancellationToken.None);
        }
        catch (Exception ex)
        {
            LogStatusUpdateFailed(_logger, testRun.Id, ex);
        }

        LogFailed(_logger, testRun.Id, errorDetail);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Starting scenario generation for test run {TestRunId} ({IssueKey})")]
    private static partial void LogStarting(ILogger logger, string testRunId, string issueKey);

    [LoggerMessage(Level = LogLevel.Information, Message = "Persisted {Count} scenarios for test run {TestRunId}")]
    private static partial void LogPersisted(ILogger logger, int count, string testRunId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Scenario generation failed for test run {TestRunId}: {ErrorDetail}")]
    private static partial void LogFailed(ILogger logger, string testRunId, string errorDetail);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to update run status to Failed for test run {TestRunId}")]
    private static partial void LogStatusUpdateFailed(ILogger logger, string testRunId, Exception ex);
}

public class ScenarioGenerationException : Exception
{
    public string TestRunId { get; }

    public ScenarioGenerationException(string testRunId, string message)
        : base(message)
    {
        TestRunId = testRunId;
    }

    public ScenarioGenerationException(string testRunId, string message, Exception innerException)
        : base(message, innerException)
    {
        TestRunId = testRunId;
    }
}
