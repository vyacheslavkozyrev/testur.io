using Microsoft.Extensions.Logging;
using Testurio.Core.Entities;
using Testurio.Core.Enums;
using Testurio.Core.Interfaces;

namespace Testurio.Infrastructure;

/// <summary>
/// Resolves the PM tool type from the project document, retrieves credentials from Key Vault,
/// then calls the appropriate PM tool client to transition the originating work item state.
/// Never throws — all errors are captured in the returned <see cref="WorkItemTransitionResult"/>.
/// </summary>
public sealed partial class WorkItemTransitionService : IWorkItemTransitionService
{
    private readonly IJiraClient _jiraClient;
    private readonly IADOClient _adoClient;
    private readonly ISecretResolver _secretResolver;
    private readonly ILogger<WorkItemTransitionService> _logger;

    public WorkItemTransitionService(
        IJiraClient jiraClient,
        IADOClient adoClient,
        ISecretResolver secretResolver,
        ILogger<WorkItemTransitionService> logger)
    {
        _jiraClient = jiraClient;
        _adoClient = adoClient;
        _secretResolver = secretResolver;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<WorkItemTransitionResult> TransitionAsync(
        Project project,
        TestRun testRun,
        string? targetStatusName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(targetStatusName))
            return new WorkItemTransitionResult(StatusTransitionOutcome.NotConfigured, null, null);

        try
        {
            return project.PmTool switch
            {
                PMToolType.Jira => await TransitionJiraAsync(project, testRun, targetStatusName, cancellationToken),
                PMToolType.Ado => await TransitionAdoAsync(project, testRun, targetStatusName, cancellationToken),
                _ => new WorkItemTransitionResult(StatusTransitionOutcome.NotConfigured, null, null)
            };
        }
        catch (Exception ex)
        {
            // Defensive catch — individual methods should not throw, but guard here too.
            LogUnexpectedError(_logger, testRun.Id, project.Id, targetStatusName, ex);
            return new WorkItemTransitionResult(StatusTransitionOutcome.Failed, null, ex.Message);
        }
    }

    private async Task<WorkItemTransitionResult> TransitionJiraAsync(
        Project project,
        TestRun testRun,
        string targetStatusName,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(project.JiraBaseUrl) || string.IsNullOrWhiteSpace(testRun.JiraIssueKey))
            return new WorkItemTransitionResult(StatusTransitionOutcome.Failed, null, "Jira base URL or issue key is missing.");

        // Resolve credentials.
        string email, apiToken;
        try
        {
            if (project.JiraAuthMethod == JiraAuthMethod.ApiToken)
            {
                if (string.IsNullOrWhiteSpace(project.JiraApiTokenSecretUri) ||
                    string.IsNullOrWhiteSpace(project.JiraEmailSecretUri))
                    return new WorkItemTransitionResult(StatusTransitionOutcome.Failed, null, "Jira credential secret references are missing.");

                apiToken = await _secretResolver.ResolveAsync(project.JiraApiTokenSecretUri, ct);
                email = await _secretResolver.ResolveAsync(project.JiraEmailSecretUri, ct);
            }
            else
            {
                // PAT auth — email field is unused by the Jira transition method but required by the interface.
                if (string.IsNullOrWhiteSpace(project.JiraPatSecretUri))
                    return new WorkItemTransitionResult(StatusTransitionOutcome.Failed, null, "Jira PAT secret reference is missing.");

                apiToken = await _secretResolver.ResolveAsync(project.JiraPatSecretUri, ct);
                email = string.Empty;
            }
        }
        catch (Exception ex)
        {
            return new WorkItemTransitionResult(StatusTransitionOutcome.Failed, null, $"Failed to retrieve Jira credentials: {ex.Message}");
        }

        var result = await _jiraClient.TransitionIssueStatusAsync(
            project.JiraBaseUrl, testRun.JiraIssueKey, email, apiToken, targetStatusName, ct);

        if (result.IsSuccess)
        {
            LogTransitionSucceeded(_logger, testRun.Id, project.Id, targetStatusName);
            return new WorkItemTransitionResult(StatusTransitionOutcome.Succeeded, targetStatusName, null);
        }

        var error = $"HTTP {result.StatusCode}: {result.ErrorDetail}";
        LogTransitionFailed(_logger, testRun.Id, project.Id, targetStatusName, error);
        return new WorkItemTransitionResult(StatusTransitionOutcome.Failed, null, error);
    }

    private async Task<WorkItemTransitionResult> TransitionAdoAsync(
        Project project,
        TestRun testRun,
        string targetStatusName,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(project.AdoOrgUrl) || string.IsNullOrWhiteSpace(project.AdoProjectName))
            return new WorkItemTransitionResult(StatusTransitionOutcome.Failed, null, "ADO org URL or project name is missing.");

        if (!int.TryParse(testRun.JiraIssueId, out var workItemId))
            return new WorkItemTransitionResult(StatusTransitionOutcome.Failed, null, $"ADO work item ID '{testRun.JiraIssueId}' is not a valid integer.");

        // Resolve token.
        string token;
        try
        {
            if (string.IsNullOrWhiteSpace(project.AdoTokenSecretUri))
                return new WorkItemTransitionResult(StatusTransitionOutcome.Failed, null, "ADO token secret reference is missing.");

            token = await _secretResolver.ResolveAsync(project.AdoTokenSecretUri, ct);
        }
        catch (Exception ex)
        {
            return new WorkItemTransitionResult(StatusTransitionOutcome.Failed, null, $"Failed to retrieve ADO token: {ex.Message}");
        }

        var result = await _adoClient.TransitionWorkItemStateAsync(
            project.AdoOrgUrl, workItemId, token, targetStatusName, ct);

        if (result.IsSuccess)
        {
            LogTransitionSucceeded(_logger, testRun.Id, project.Id, targetStatusName);
            return new WorkItemTransitionResult(StatusTransitionOutcome.Succeeded, targetStatusName, null);
        }

        var error = $"HTTP {result.StatusCode}: {result.ErrorDetail}";
        LogTransitionFailed(_logger, testRun.Id, project.Id, targetStatusName, error);
        return new WorkItemTransitionResult(StatusTransitionOutcome.Failed, null, error);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Work item status transition succeeded for run {RunId} in project {ProjectId} — transitioned to '{TargetStatus}'")]
    private static partial void LogTransitionSucceeded(ILogger logger, string runId, string projectId, string targetStatus);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Work item status transition failed for run {RunId} in project {ProjectId} — target '{TargetStatus}': {Error}")]
    private static partial void LogTransitionFailed(ILogger logger, string runId, string projectId, string targetStatus, string error);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Unexpected error during work item status transition for run {RunId} in project {ProjectId} — target '{TargetStatus}'")]
    private static partial void LogUnexpectedError(ILogger logger, string runId, string projectId, string targetStatus, Exception ex);
}
