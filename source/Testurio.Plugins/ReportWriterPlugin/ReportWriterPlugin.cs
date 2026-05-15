using Microsoft.Extensions.Logging;
using Testurio.Core.Entities;
using Testurio.Core.Enums;
using Testurio.Core.Interfaces;
using Testurio.Core.Repositories;
using Testurio.Infrastructure.Blob;

namespace Testurio.Plugins.ReportWriterPlugin;

/// <summary>
/// Loads run, scenario, step result, and execution log data; builds a Jira markdown report;
/// and posts it as a comment on the originating Jira issue.
/// Sets run status to <see cref="TestRunStatus.ReportDeliveryFailed"/> on any
/// Jira API error (AC-013, AC-014).
/// </summary>
public partial class ReportWriterPlugin
{
    private readonly ITestRunRepository _testRunRepository;
    private readonly ITestScenarioRepository _scenarioRepository;
    private readonly IStepResultRepository _stepResultRepository;
    private readonly IExecutionLogRepository _executionLogRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly IJiraApiClient _jiraApiClient;
    private readonly ISecretResolver _secretResolver;
    private readonly ReportBuilderService _reportBuilder;
    private readonly TemplateRepository _templateRepository;
    private readonly IBlobStorageClient _blobStorageClient;
    private readonly ILogger<ReportWriterPlugin> _logger;

    public ReportWriterPlugin(
        ITestRunRepository testRunRepository,
        ITestScenarioRepository scenarioRepository,
        IStepResultRepository stepResultRepository,
        IExecutionLogRepository executionLogRepository,
        IProjectRepository projectRepository,
        IJiraApiClient jiraApiClient,
        ISecretResolver secretResolver,
        ReportBuilderService reportBuilder,
        TemplateRepository templateRepository,
        IBlobStorageClient blobStorageClient,
        ILogger<ReportWriterPlugin> logger)
    {
        _testRunRepository = testRunRepository;
        _scenarioRepository = scenarioRepository;
        _stepResultRepository = stepResultRepository;
        _executionLogRepository = executionLogRepository;
        _projectRepository = projectRepository;
        _jiraApiClient = jiraApiClient;
        _secretResolver = secretResolver;
        _reportBuilder = reportBuilder;
        _templateRepository = templateRepository;
        _blobStorageClient = blobStorageClient;
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
        var logEntries = await _executionLogRepository.GetByRunAsync(projectId, testRunId, cancellationToken);

        // AC-028–AC-034: load custom template or fall back to built-in default.
        var template = await LoadTemplateAsync(project, run, cancellationToken);

        // AC-031–AC-032: render the template with run data and attachment toggle settings.
        var commentBody = _reportBuilder.BuildFromTemplate(
            template,
            run,
            scenarios,
            stepResults,
            logEntries,
            reportIncludeLogs: project.ReportIncludeLogs,
            reportIncludeScreenshots: project.ReportIncludeScreenshots);

        // AC-033: store the rendered report as a blob and persist the URI on the run record.
        var reportBlobUri = await _blobStorageClient.UploadAsync(
            $"reports/{projectId}/{testRunId}/report.md",
            commentBody,
            cancellationToken);

        if (!string.IsNullOrEmpty(reportBlobUri))
        {
            run.ReportBlobUri = reportBlobUri;
            await _testRunRepository.UpdateAsync(run, cancellationToken);
        }

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

    /// <summary>
    /// Loads the custom template from blob storage if <paramref name="project"/> has one;
    /// falls back to the built-in default on any error (AC-034).
    /// Records a warning on the run when falling back due to a fetch error.
    /// </summary>
    private async Task<string> LoadTemplateAsync(
        Project project,
        TestRun run,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(project.ReportTemplateUri))
            return DefaultReportTemplate.Content;

        var templateContent = await _templateRepository.DownloadAsync(project.ReportTemplateUri, cancellationToken);
        if (templateContent is not null)
            return templateContent;

        // AC-034: blob fetch failed — fall back to default and record warning.
        var warning = $"Custom template blob could not be fetched ({project.ReportTemplateUri}); built-in default used.";
        LogTemplateFetchFailed(_logger, run.Id, project.ReportTemplateUri);
        run.ReportTemplateWarning = warning;
        await _testRunRepository.UpdateAsync(run, cancellationToken);

        return DefaultReportTemplate.Content;
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

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to fetch custom template blob for run {TestRunId}: {BlobUri} — using built-in default")]
    private static partial void LogTemplateFetchFailed(ILogger logger, string testRunId, string blobUri);
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
