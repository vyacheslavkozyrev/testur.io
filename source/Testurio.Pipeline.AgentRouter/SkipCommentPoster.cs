using Microsoft.Extensions.Logging;
using Testurio.Core.Entities;
using Testurio.Core.Enums;
using Testurio.Core.Interfaces;

namespace Testurio.Pipeline.AgentRouter;

/// <summary>
/// Posts a skip comment to the originating PM tool ticket (ADO or Jira) when the AgentRouter
/// cannot resolve any applicable test type for a story.
/// Fire-and-forget — all exceptions are caught and logged so the pipeline continues uninterrupted.
/// </summary>
public sealed partial class SkipCommentPoster
{
    private const string CommentTemplate =
        """
        **Testurio — Test Run Skipped**

        No applicable test type could be determined for this story.

        **Classification reason:** {0}

        **Suggestions:**
        - Review the story's acceptance criteria to ensure they describe concrete, testable behaviour.
        - Check the project's test type configuration (API / UI E2E / Both) in Testurio project settings.

        This run has been marked as **Skipped — no applicable test type** and will not affect pass-rate metrics.
        """;

    private readonly IJiraApiClient _jiraApiClient;
    private readonly IADOClient _adoClient;
    private readonly ISecretResolver _secretResolver;
    private readonly ILogger<SkipCommentPoster> _logger;

    public SkipCommentPoster(
        IJiraApiClient jiraApiClient,
        IADOClient adoClient,
        ISecretResolver secretResolver,
        ILogger<SkipCommentPoster> logger)
    {
        _jiraApiClient = jiraApiClient;
        _adoClient = adoClient;
        _secretResolver = secretResolver;
        _logger = logger;
    }

    /// <summary>
    /// Posts the skip notification comment to the originating work item asynchronously.
    /// A failure to post does not throw — the error is logged and the method returns normally.
    /// </summary>
    /// <param name="testRun">The test run that is being skipped.</param>
    /// <param name="project">The project configuration containing PM tool credentials.</param>
    /// <param name="classificationReason">Claude's rationale for not classifying the story.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task PostSkipCommentAsync(
        TestRun testRun,
        Project project,
        string classificationReason,
        CancellationToken ct = default)
    {
        var commentBody = string.Format(CommentTemplate, classificationReason);
        var pmToolType = project.PmTool ?? PMToolType.Jira;

        try
        {
            if (pmToolType == PMToolType.Jira)
                await PostJiraSkipCommentAsync(testRun, project, commentBody, ct);
            else
                await PostAdoSkipCommentAsync(testRun, project, commentBody, ct);
        }
        catch (Exception ex)
        {
            // AC-008: comment-post failure must not throw — log and return normally.
            LogCommentPostFailed(_logger, testRun.JiraIssueKey, pmToolType.ToString(), ex);
        }
    }

    private async Task PostJiraSkipCommentAsync(
        TestRun testRun,
        Project project,
        string commentBody,
        CancellationToken ct)
    {
        if (string.IsNullOrEmpty(project.JiraEmailSecretUri) || string.IsNullOrEmpty(project.JiraApiTokenSecretUri))
        {
            LogJiraCredentialsMissing(_logger, testRun.JiraIssueKey);
            return;
        }

        var email = await _secretResolver.ResolveAsync(project.JiraEmailSecretUri, ct);
        var token = await _secretResolver.ResolveAsync(project.JiraApiTokenSecretUri, ct);

        var result = await _jiraApiClient.PostCommentAsync(
            project.JiraBaseUrl!,
            testRun.JiraIssueKey,
            email,
            token,
            commentBody,
            ct);

        if (!result.IsSuccess)
            LogJiraCommentFailed(_logger, testRun.JiraIssueKey, result.StatusCode, result.ErrorDetail ?? string.Empty);
        else
            LogCommentPosted(_logger, testRun.JiraIssueKey, "Jira");
    }

    private async Task PostAdoSkipCommentAsync(
        TestRun testRun,
        Project project,
        string commentBody,
        CancellationToken ct)
    {
        if (!int.TryParse(testRun.JiraIssueId, out var workItemId))
        {
            LogAdoWorkItemIdMissing(_logger, testRun.JiraIssueKey);
            return;
        }

        if (string.IsNullOrEmpty(project.AdoTokenSecretUri))
        {
            LogAdoCredentialsMissing(_logger, testRun.JiraIssueKey);
            return;
        }

        var token = await _secretResolver.ResolveAsync(project.AdoTokenSecretUri, ct);

        var success = await _adoClient.PostCommentAsync(
            project.AdoOrgUrl!,
            project.AdoProjectName!,
            workItemId,
            token,
            commentBody,
            ct);

        if (!success)
            LogAdoCommentFailed(_logger, testRun.JiraIssueKey);
        else
            LogCommentPosted(_logger, testRun.JiraIssueKey, "ADO");
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "SkipCommentPoster: Jira credentials not configured (Key Vault URI missing) for {IssueKey} — comment skipped")]
    private static partial void LogJiraCredentialsMissing(ILogger logger, string issueKey);

    [LoggerMessage(Level = LogLevel.Warning, Message = "SkipCommentPoster: failed to post skip comment to {IssueKey} ({PmToolType})")]
    private static partial void LogCommentPostFailed(ILogger logger, string issueKey, string pmToolType, Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "SkipCommentPoster: Jira comment post failed for {IssueKey} (HTTP {StatusCode}): {ErrorDetail}")]
    private static partial void LogJiraCommentFailed(ILogger logger, string issueKey, int statusCode, string errorDetail);

    [LoggerMessage(Level = LogLevel.Warning, Message = "SkipCommentPoster: ADO comment post failed for {IssueKey}")]
    private static partial void LogAdoCommentFailed(ILogger logger, string issueKey);

    [LoggerMessage(Level = LogLevel.Warning, Message = "SkipCommentPoster: ADO token not configured (Key Vault URI missing) for {IssueKey} — comment skipped")]
    private static partial void LogAdoCredentialsMissing(ILogger logger, string issueKey);

    [LoggerMessage(Level = LogLevel.Warning, Message = "SkipCommentPoster: ADO work item id could not be parsed for issue key {IssueKey} — comment skipped")]
    private static partial void LogAdoWorkItemIdMissing(ILogger logger, string issueKey);

    [LoggerMessage(Level = LogLevel.Information, Message = "SkipCommentPoster: skip comment posted to {IssueKey} ({PmToolType})")]
    private static partial void LogCommentPosted(ILogger logger, string issueKey, string pmToolType);
}
