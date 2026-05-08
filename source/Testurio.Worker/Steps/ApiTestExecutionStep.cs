using Microsoft.Extensions.Logging;
using Testurio.Core.Entities;
using Testurio.Core.Enums;
using Testurio.Core.Repositories;
using Testurio.Infrastructure.KeyVault;
using Testurio.Plugins.TestExecutorPlugin;

namespace Testurio.Worker.Steps;

/// <summary>
/// Orchestrates API test execution for all scenarios in a test run.
/// Dispatches all steps per scenario in parallel (AC-001), collects results,
/// persists them (AC-016), aggregates the run-level pass/fail status (AC-017),
/// and returns the step results for the report delivery step (AC-018).
/// </summary>
public partial class ApiTestExecutionStep
{
    private readonly TestExecutorPlugin _testExecutorPlugin;
    private readonly KeyVaultCredentialClient _credentialClient;
    private readonly IStepResultRepository _stepResultRepository;
    private readonly ITestRunRepository _testRunRepository;
    private readonly ILogger<ApiTestExecutionStep> _logger;

    public ApiTestExecutionStep(
        TestExecutorPlugin testExecutorPlugin,
        KeyVaultCredentialClient credentialClient,
        IStepResultRepository stepResultRepository,
        ITestRunRepository testRunRepository,
        ILogger<ApiTestExecutionStep> logger)
    {
        _testExecutorPlugin = testExecutorPlugin;
        _credentialClient = credentialClient;
        _stepResultRepository = stepResultRepository;
        _testRunRepository = testRunRepository;
        _logger = logger;
    }

    /// <summary>
    /// Executes all scenarios for the given test run and persists results.
    /// Returns the full list of step results so the report delivery step can use them.
    /// </summary>
    public async Task<IReadOnlyList<StepResult>> ExecuteAsync(
        TestRun testRun,
        Project project,
        IReadOnlyList<TestScenario> scenarios,
        CancellationToken cancellationToken = default)
    {
        LogStarting(_logger, testRun.Id, scenarios.Count);

        // Resolve Bearer token once for all scenarios in this run (AC-006, AC-007, AC-008).
        var bearerToken = await _credentialClient.ResolveBearerTokenAsync(
            project.BearerTokenSecretRef, cancellationToken);

        var allResults = new List<StepResult>();

        // AC-004: execute all scenarios regardless of individual scenario outcomes.
        foreach (var scenario in scenarios)
        {
            LogExecutingScenario(_logger, scenario.Title, scenario.Id, testRun.Id);

            // AC-001/AC-003: all steps in a scenario run in parallel; all are attempted.
            var scenarioResults = await _testExecutorPlugin.ExecuteScenarioAsync(
                scenario, project.ProductUrl, bearerToken, cancellationToken);

            allResults.AddRange(scenarioResults);
        }

        // AC-016: persist all step results.
        await _stepResultRepository.CreateBatchAsync(allResults, cancellationToken);
        LogResultsPersisted(_logger, allResults.Count, testRun.Id);

        // AC-017: aggregate run status — Passed only if every step passed.
        var allPassed = allResults.Count > 0 && allResults.All(r => r.Status == StepStatus.Passed);
        testRun.Status = allPassed ? TestRunStatus.Completed : TestRunStatus.Failed;
        await _testRunRepository.UpdateAsync(testRun, cancellationToken);

        LogRunStatusSet(_logger, testRun.Id, testRun.Status.ToString());

        // AC-018: return results so the report delivery step (feature 0004) can consume them.
        return allResults.AsReadOnly();
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Starting API test execution for run {TestRunId} with {ScenarioCount} scenarios")]
    private static partial void LogStarting(ILogger logger, string testRunId, int scenarioCount);

    [LoggerMessage(Level = LogLevel.Information, Message = "Executing scenario '{ScenarioTitle}' ({ScenarioId}) for run {TestRunId}")]
    private static partial void LogExecutingScenario(ILogger logger, string scenarioTitle, string scenarioId, string testRunId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Persisted {ResultCount} step results for run {TestRunId}")]
    private static partial void LogResultsPersisted(ILogger logger, int resultCount, string testRunId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Run {TestRunId} status set to {Status}")]
    private static partial void LogRunStatusSet(ILogger logger, string testRunId, string status);
}
