using Microsoft.Extensions.Logging;
using Testurio.Core.Enums;
using Testurio.Core.Interfaces;
using Testurio.Core.Models;

namespace Testurio.Pipeline.StoryParser;

/// <summary>
/// Posts a warning comment to the originating PM tool ticket (ADO or Jira) when the AI-conversion
/// path is taken. Fire-and-forget — failures are logged and swallowed so the pipeline continues.
/// </summary>
public sealed partial class PmToolCommentPoster
{
    private const string DocumentationUrl = "https://docs.testurio.io/story-template";

    private const string WarningTemplate =
        """
        **Testurio — Story Template Warning**

        This work item did not match the Testurio story template, so AI conversion was applied to extract the required fields automatically.

        For best results and to avoid this conversion step on future runs, please update the story to follow the required template format.

        See the documentation for the expected format: {0}
        """;

    private readonly IJiraApiClient _jiraApiClient;
    private readonly IADOClient _adoClient;
    private readonly ISecretResolver _secretResolver;
    private readonly ILogger<PmToolCommentPoster> _logger;

    public PmToolCommentPoster(
        IJiraApiClient jiraApiClient,
        IADOClient adoClient,
        ISecretResolver secretResolver,
        ILogger<PmToolCommentPoster> logger)
    {
        _jiraApiClient = jiraApiClient;
        _adoClient = adoClient;
        _secretResolver = secretResolver;
        _logger = logger;
    }

    /// <summary>
    /// Posts the template-warning comment to the originating work item asynchronously.
    /// A failure to post does not throw — the error is logged and the method returns normally.
    /// </summary>
    /// <param name="workItem">The work item that triggered the AI-conversion path.</param>
    /// <param name="project">The project configuration containing PM tool credentials.</param>
    public async Task PostWarningAsync(WorkItem workItem, Core.Entities.Project project, CancellationToken ct = default)
    {
        var commentBody = string.Format(WarningTemplate, DocumentationUrl);

        try
        {
            if (workItem.PmToolType == PMToolType.Jira)
                await PostJiraWarningAsync(workItem, project, commentBody, ct);
            else
                await PostAdoWarningAsync(workItem, project, commentBody, ct);
        }
        catch (Exception ex)
        {
            // AC-014: comment-post failure must not halt the pipeline.
            LogCommentPostFailed(_logger, workItem.IssueKey, workItem.PmToolType.ToString(), ex);
        }
    }

    private async Task PostJiraWarningAsync(WorkItem workItem, Core.Entities.Project project, string commentBody, CancellationToken ct)
    {
        // Credentials must come from Key Vault only — never from plaintext Project fields.
        // Architecture rule: only the Key Vault secret URI is stored in Cosmos; the value is never.
        if (string.IsNullOrEmpty(project.JiraEmailSecretUri))
        {
            LogJiraCredentialsMissing(_logger, workItem.IssueKey);
            return;
        }

        if (string.IsNullOrEmpty(project.JiraApiTokenSecretUri))
        {
            LogJiraCredentialsMissing(_logger, workItem.IssueKey);
            return;
        }

        var email = await _secretResolver.ResolveAsync(project.JiraEmailSecretUri, ct);
        var token = await _secretResolver.ResolveAsync(project.JiraApiTokenSecretUri, ct);

        var result = await _jiraApiClient.PostCommentAsync(
            project.JiraBaseUrl!,
            workItem.IssueKey,
            email,
            token,
            commentBody,
            ct);

        if (!result.IsSuccess)
            LogJiraCommentFailed(_logger, workItem.IssueKey, result.StatusCode, result.ErrorDetail ?? string.Empty);
        else
            LogCommentPosted(_logger, workItem.IssueKey, "Jira");
    }

    private async Task PostAdoWarningAsync(WorkItem workItem, Core.Entities.Project project, string commentBody, CancellationToken ct)
    {
        if (workItem.AdoWorkItemId is null)
        {
            LogAdoWorkItemIdMissing(_logger, workItem.IssueKey);
            return;
        }

        var token = await _secretResolver.ResolveAsync(project.AdoTokenSecretUri ?? string.Empty, ct);

        var success = await _adoClient.PostCommentAsync(
            project.AdoOrgUrl!,
            project.AdoProjectName!,
            workItem.AdoWorkItemId.Value,
            token,
            commentBody,
            ct);

        if (!success)
            LogAdoCommentFailed(_logger, workItem.IssueKey);
        else
            LogCommentPosted(_logger, workItem.IssueKey, "ADO");
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "PmToolCommentPoster: Jira credentials not configured (Key Vault URI missing) for {IssueKey} — comment skipped")]
    private static partial void LogJiraCredentialsMissing(ILogger logger, string issueKey);

    [LoggerMessage(Level = LogLevel.Warning, Message = "PmToolCommentPoster: failed to post warning comment to {IssueKey} ({PmToolType})")]
    private static partial void LogCommentPostFailed(ILogger logger, string issueKey, string pmToolType, Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "PmToolCommentPoster: Jira comment post failed for {IssueKey} (HTTP {StatusCode}): {ErrorDetail}")]
    private static partial void LogJiraCommentFailed(ILogger logger, string issueKey, int statusCode, string errorDetail);

    [LoggerMessage(Level = LogLevel.Warning, Message = "PmToolCommentPoster: ADO comment post failed for {IssueKey}")]
    private static partial void LogAdoCommentFailed(ILogger logger, string issueKey);

    [LoggerMessage(Level = LogLevel.Warning, Message = "PmToolCommentPoster: ADO work item id missing for issue key {IssueKey} — comment skipped")]
    private static partial void LogAdoWorkItemIdMissing(ILogger logger, string issueKey);

    [LoggerMessage(Level = LogLevel.Information, Message = "PmToolCommentPoster: warning comment posted to {IssueKey} ({PmToolType})")]
    private static partial void LogCommentPosted(ILogger logger, string issueKey, string pmToolType);
}
