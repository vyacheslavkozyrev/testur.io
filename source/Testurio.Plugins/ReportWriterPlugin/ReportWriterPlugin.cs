using Microsoft.Extensions.Logging;
using Testurio.Core.Entities;
using Testurio.Core.Enums;
using Testurio.Core.Interfaces;
using Testurio.Core.Repositories;

namespace Testurio.Plugins.ReportWriterPlugin;

/// <summary>
/// Loads run, scenario, and step result data; builds a Jira markdown report;
/// and posts it as a comment on the originating Jira issue.
/// Sets run status to <see cref="TestRunStatus.ReportDeliveryFailed"/> on any
/// Jira API error (AC-013, AC-014).
/// </summary>
public partial class ReportWriterPlugin
{
    private readonly ITestRunRepository _testRunRepository;
    private readonly ITestScenarioRepository _scenarioRepository;
    private readonly IStepResultRepository _stepResultRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly IJiraApiClient _jiraApiClient;
    private readonly ISecretResolver _secretResolver;
    private readonly ReportBuilderService _reportBuilder;
    private readonly ILogger<ReportWriterPlugin> _logger;

    public ReportWriterPlugin(
        ITestRunRepository testRunRepository,
        ITestScenarioRepository scenarioRepository,
        IStepResultRepository stepResultRepository,
        IProjectRepository projectRepository,
        IJiraApiClient jiraApiClient,
        ISecretResolver secretResolver,
        ReportBuilderService reportBuilder,
        ILogger<ReportWriterPlugin> logger)
    {
        _testRunRepository = testRunRepository;
        _scenarioRepository = scenarioRepository;
        _stepResultRepository = stepResultRepository;
        _projectRepository = projectRepository;
        _jiraApiClient = jiraApiClient;
        _secretResolver = secretResolver;
        _reportBuilder = reportBuilder;
        _logger = logger;
    }

    /// <summary>
    /// Delivers a report comment to the Jira issue that triggered this run.
    /// Returns <c>true</c> on successful delivery; <c>false</c> if delivery failed
    /// (run status is updated to <see cref="TestRunStatus.ReportDeliveryFailed"/> by the caller).
    /// </summary>
    public async Task<ReportDeliveryResult> DeliverAsync(
        string projectId,
        string testRunId,
        CancellationToken cancellationToken = default)
    {
        var run = await _testRunRepository.GetByIdAsync(projectId, testRunId, cancellationToken);
        if (run is null)
        {
            var msg = $"TestRun {testRunId} not found in project {projectId}";
            LogRunNotFound(_logger, testRunId, projectId);
            return ReportDeliveryResult.Failure(msg);
        }

        var project = await _projectRepository.GetByProjectIdAsync(projectId, cancellationToken);
        if (project is null)
        {
            var msg = $"Project {projectId} not found";
            LogProjectNotFound(_logger, projectId);
            return ReportDeliveryResult.Failure(msg);
        }

        var scenarios = await _scenarioRepository.GetByRunAsync(projectId, testRunId, cancellationToken);
        var stepResults = await _stepResultRepository.GetByRunAsync(projectId, testRunId, cancellationToken);

        var commentBody = _reportBuilder.Build(run, scenarios, stepResults);

        string apiToken;
        try
        {
            apiToken = await _secretResolver.ResolveAsync(project.JiraApiTokenSecretRef!, cancellationToken);
        }
        catch (Exception ex)
        {
            var msg = $"Failed to resolve Jira API token secret: {ex.Message}";
            LogSecretResolutionFailed(_logger, project.Id, ex);
            return ReportDeliveryResult.Failure(msg);
        }

        var commentResult = await _jiraApiClient.PostCommentAsync(
            project.JiraBaseUrl!,
            run.JiraIssueKey,
            project.JiraEmail!,
            apiToken,
            commentBody,
            cancellationToken);

        if (!commentResult.IsSuccess)
        {
            // Include HTTP status code and Jira error body in the failure message so the
            // caller can persist it in TestRun.DeliveryError for run-history visibility (AC-014).
            var msg = commentResult.StatusCode > 0
                ? $"Jira API rejected comment post for issue {run.JiraIssueKey}: HTTP {commentResult.StatusCode} — {commentResult.ErrorDetail}"
                : $"Network error posting comment for issue {run.JiraIssueKey}: {commentResult.ErrorDetail}";
            LogDeliveryFailed(_logger, testRunId, run.JiraIssueKey);
            return ReportDeliveryResult.Failure(msg);
        }

        LogDelivered(_logger, testRunId, run.JiraIssueKey);
        return ReportDeliveryResult.Success();
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "TestRun {TestRunId} not found in project {ProjectId} during report delivery")]
    private static partial void LogRunNotFound(ILogger logger, string testRunId, string projectId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Project {ProjectId} not found during report delivery")]
    private static partial void LogProjectNotFound(ILogger logger, string projectId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to resolve Jira API token for project {ProjectId}")]
    private static partial void LogSecretResolutionFailed(ILogger logger, string projectId, Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Report delivery failed for run {TestRunId} on Jira issue {IssueKey}")]
    private static partial void LogDeliveryFailed(ILogger logger, string testRunId, string issueKey);

    [LoggerMessage(Level = LogLevel.Information, Message = "Report delivered for run {TestRunId} to Jira issue {IssueKey}")]
    private static partial void LogDelivered(ILogger logger, string testRunId, string issueKey);
}

public class ReportDeliveryResult
{
    public bool IsSuccess { get; private init; }
    public string? ErrorMessage { get; private init; }

    private ReportDeliveryResult() { }

    public static ReportDeliveryResult Success() => new() { IsSuccess = true };

    public static ReportDeliveryResult Failure(string errorMessage) =>
        new() { IsSuccess = false, ErrorMessage = errorMessage };
}
