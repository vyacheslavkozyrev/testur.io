using Microsoft.Extensions.Logging;
using Testurio.Core.Entities;
using Testurio.Core.Enums;
using Testurio.Core.Interfaces;
using Testurio.Core.Models;
using Testurio.Core.Repositories;

namespace Testurio.Worker.Steps;

/// <summary>
/// Stage 7 of the pipeline (feature 0024).
/// After <see cref="IReportWriter.WriteAsync"/> succeeds, this step transitions the originating
/// PM tool work item to the configured "on pass" or "on fail" status.
/// Never throws — transition failures are recorded on the <see cref="TestRun"/> and logged;
/// the pipeline always continues regardless of the outcome.
/// </summary>
public sealed partial class WorkItemTransitionStep
{
    private readonly IWorkItemTransitionService _transitionService;
    private readonly ITestRunRepository _testRunRepository;
    private readonly ILogger<WorkItemTransitionStep> _logger;

    public WorkItemTransitionStep(
        IWorkItemTransitionService transitionService,
        ITestRunRepository testRunRepository,
        ILogger<WorkItemTransitionStep> logger)
    {
        _transitionService = transitionService;
        _testRunRepository = testRunRepository;
        _logger = logger;
    }

    /// <summary>
    /// Executes the transition step. Determines pass/fail from <paramref name="executionResult"/>,
    /// selects the configured target status from <paramref name="project"/>, calls
    /// <see cref="IWorkItemTransitionService.TransitionAsync"/>, and updates
    /// <paramref name="testRun"/> transition fields in Cosmos.
    /// </summary>
    public async Task ExecuteAsync(
        TestRun testRun,
        Project project,
        ExecutionResult executionResult,
        CancellationToken cancellationToken = default)
    {
        // Determine whether the run passed (all scenarios passed) or failed (at least one failed).
        var allPassed =
            executionResult.ApiResults.All(r => r.Passed) &&
            executionResult.UiE2eResults.All(r => r.Passed);

        // Select the configured target status based on the run outcome.
        var targetStatus = allPassed
            ? SelectPassedStatus(project)
            : SelectFailedStatus(project);

        // Attempt the transition (never throws).
        var result = await _transitionService.TransitionAsync(project, testRun, targetStatus, cancellationToken);

        // Update the TestRun with the transition outcome.
        testRun.StatusTransitionOutcome = result.Outcome;
        testRun.StatusTransitionedTo = result.TransitionedTo;
        testRun.StatusTransitionError = result.ErrorDetail;

        // Persist the outcome — use CancellationToken.None so a cancellation at this point
        // does not leave the TestRun without a transition outcome recorded.
        try
        {
            await _testRunRepository.UpdateAsync(testRun, CancellationToken.None);
        }
        catch (Exception ex)
        {
            // Non-fatal — the run status has already been persisted; log and continue.
            LogOutcomeUpdateFailed(_logger, testRun.Id, ex);
        }
    }

    private static string? SelectPassedStatus(Project project) =>
        project.PmTool switch
        {
            PMToolType.Jira => project.JiraPassedTransitionStatus,
            PMToolType.Ado  => project.AdoPassedTransitionStatus,
            _               => null
        };

    private static string? SelectFailedStatus(Project project) =>
        project.PmTool switch
        {
            PMToolType.Jira => project.JiraFailedTransitionStatus,
            PMToolType.Ado  => project.AdoFailedTransitionStatus,
            _               => null
        };

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to persist transition outcome for run {RunId}")]
    private static partial void LogOutcomeUpdateFailed(ILogger logger, string runId, Exception ex);
}
