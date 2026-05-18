using Microsoft.Extensions.Logging;
using Testurio.Core.Enums;
using Testurio.Core.Interfaces;
using Testurio.Core.Models;
using Testurio.Core.Repositories;

namespace Testurio.Pipeline.FeedbackLoop;

/// <summary>
/// Stage 7 of the pipeline (feature 0031).
/// Processes incoming comment-created webhook events forwarded via the
/// <c>testurio-comment-events</c> Service Bus topic:
/// <list type="number">
///   <item>Detects the <c>@testurio memorize</c> flag (case-insensitive substring match).</item>
///   <item>Strips the flag and trims the remainder to produce <c>feedbackText</c>.</item>
///   <item>Resolves the last pipeline run for the work item to determine active test types.</item>
///   <item>For each resolved test type: embeds <c>feedbackText</c> and upserts a <c>TestMemory</c>
///         document tagged <c>source: qalead</c>.</item>
///   <item>Posts a single confirmation comment to the originating PM tool ticket.</item>
/// </list>
/// Embedding and Cosmos write failures are rethrown so the Service Bus message is not settled
/// and can be retried. PM tool confirmation-post failures are swallowed with a warning log.
/// </summary>
public sealed partial class FeedbackLoop : IFeedbackLoop
{
    private const string FeedbackFlag = "@testurio memorize";

    private readonly ITestRunRepository _testRunRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly IEmbeddingService _embeddingService;
    private readonly ITestMemoryRepository _testMemoryRepository;
    private readonly IJiraApiClient _jiraApiClient;
    private readonly IADOClient _adoClient;
    private readonly ISecretResolver _secretResolver;
    private readonly ILogger<FeedbackLoop> _logger;

    public FeedbackLoop(
        ITestRunRepository testRunRepository,
        IProjectRepository projectRepository,
        IEmbeddingService embeddingService,
        ITestMemoryRepository testMemoryRepository,
        IJiraApiClient jiraApiClient,
        IADOClient adoClient,
        ISecretResolver secretResolver,
        ILogger<FeedbackLoop> logger)
    {
        _testRunRepository = testRunRepository;
        _projectRepository = projectRepository;
        _embeddingService = embeddingService;
        _testMemoryRepository = testMemoryRepository;
        _jiraApiClient = jiraApiClient;
        _adoClient = adoClient;
        _secretResolver = secretResolver;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task ProcessAsync(CommentWebhookEvent evt, CancellationToken ct)
    {
        // AC-002 / AC-003: case-insensitive flag detection.
        var flagIndex = evt.CommentBody.IndexOf(FeedbackFlag, StringComparison.OrdinalIgnoreCase);
        if (flagIndex < 0)
            return;

        // AC-004: strip the exact matched substring (case-preserving remainder) and trim.
        var feedbackText = (evt.CommentBody[..flagIndex] + evt.CommentBody[(flagIndex + FeedbackFlag.Length)..]).Trim();
        if (string.IsNullOrEmpty(feedbackText))
            return;

        // AC-006: check cancellation before first I/O.
        ct.ThrowIfCancellationRequested();

        // Load the project to get credentials for the PM tool comment post.
        var project = await _projectRepository.GetByProjectIdAsync(evt.ProjectId.ToString(), ct);
        if (project is null)
        {
            LogProjectNotFound(_logger, evt.ProjectId.ToString(), evt.CommentId);
            return;
        }

        // AC-007 / AC-008 / AC-010: resolve test types from the last run for this work item.
        var lastRun = await _testRunRepository.GetMostRecentByWorkItemAsync(
            evt.ProjectId.ToString(), evt.WorkItemId, ct);

        if (lastRun is null)
        {
            LogNoRunFound(_logger, evt.WorkItemId, evt.ProjectId.ToString(), evt.CommentId);
            return;
        }

        var resolvedTestTypes = lastRun.ResolvedTestTypes ?? [];
        if (resolvedTestTypes.Length == 0)
        {
            LogNoTestTypes(_logger, evt.WorkItemId, evt.ProjectId.ToString(), evt.CommentId);
            return;
        }

        // AC-006: check cancellation before embedding.
        ct.ThrowIfCancellationRequested();

        // AC-009 / AC-011: embed once; upsert per resolved test type.
        // A failure on one test type does not skip the remaining types.
        float[]? embedding = null;
        try
        {
            embedding = await _embeddingService.EmbedAsync(feedbackText, ct);
        }
        catch (Exception ex)
        {
            // AC-017: log and rethrow — Service Bus message not settled, will be retried.
            LogEmbeddingFailed(_logger, evt.WorkItemId, evt.CommentId, ex.Message, ex);
            throw;
        }

        foreach (var testType in resolvedTestTypes)
        {
            var testTypeString = testType.ToString().ToLowerInvariant();

            // AC-006: check cancellation before each upsert.
            ct.ThrowIfCancellationRequested();

            try
            {
                await _testMemoryRepository.UpsertFeedbackAsync(
                    userId: project.UserId,
                    projectId: evt.ProjectId,
                    testType: testTypeString,
                    feedbackText: feedbackText,
                    storyEmbedding: embedding,
                    workItemId: evt.WorkItemId,
                    commentId: evt.CommentId,
                    cancellationToken: ct);

                LogUpserted(_logger, evt.WorkItemId, evt.CommentId, testTypeString);
            }
            catch (Exception ex)
            {
                // AC-018: log and rethrow — Service Bus message not settled, will be retried.
                LogUpsertFailed(_logger, evt.WorkItemId, evt.CommentId, testTypeString, ex.Message, ex);
                throw;
            }
        }

        // AC-019 / AC-020 / AC-021 / AC-023: post a single confirmation reply.
        // AC-006: check cancellation before PM tool call.
        ct.ThrowIfCancellationRequested();

        var testTypeList = string.Join(", ", resolvedTestTypes.Select(t => t.ToString().ToLowerInvariant()));
        var confirmationBody =
            $"✅ **Feedback captured.** Your note has been stored and will be used in future {testTypeList} test generation for this ticket.";

        try
        {
            await PostConfirmationCommentAsync(project, evt, confirmationBody, ct);
        }
        catch (Exception ex)
        {
            // AC-022: PM tool post failure is non-fatal — log warning, do not rethrow.
            LogConfirmationPostFailed(_logger, evt.WorkItemId, evt.CommentId, evt.PmTool, ex.Message);
        }
    }

    // ─── Private helpers ──────────────────────────────────────────────────────

    private async Task PostConfirmationCommentAsync(
        Core.Entities.Project project,
        CommentWebhookEvent evt,
        string commentBody,
        CancellationToken ct)
    {
        var pmToolType = project.PmTool ?? PMToolType.Jira;

        if (string.Equals(evt.PmTool, "jira", StringComparison.OrdinalIgnoreCase) ||
            pmToolType == PMToolType.Jira)
        {
            await PostJiraConfirmationAsync(project, evt.WorkItemId, commentBody, ct);
        }
        else
        {
            await PostAdoConfirmationAsync(project, evt.WorkItemId, commentBody, ct);
        }
    }

    private async Task PostJiraConfirmationAsync(
        Core.Entities.Project project,
        string issueKey,
        string commentBody,
        CancellationToken ct)
    {
        if (string.IsNullOrEmpty(project.JiraEmailSecretUri) || string.IsNullOrEmpty(project.JiraApiTokenSecretUri))
        {
            LogJiraCredentialsMissing(_logger, issueKey, project.Id);
            return;
        }

        var email = await _secretResolver.ResolveAsync(project.JiraEmailSecretUri, ct);
        var token = await _secretResolver.ResolveAsync(project.JiraApiTokenSecretUri, ct);

        var result = await _jiraApiClient.PostCommentAsync(
            project.JiraBaseUrl!, issueKey, email, token, commentBody, ct);

        if (!result.IsSuccess)
            LogCommentPostWarning(_logger, issueKey, "Jira", result.StatusCode, result.ErrorDetail ?? string.Empty);
    }

    private async Task PostAdoConfirmationAsync(
        Core.Entities.Project project,
        string workItemId,
        string commentBody,
        CancellationToken ct)
    {
        if (!int.TryParse(workItemId, out var workItemIntId))
        {
            LogAdoWorkItemIdMissing(_logger, workItemId, project.Id);
            return;
        }

        if (string.IsNullOrEmpty(project.AdoTokenSecretUri))
        {
            LogAdoCredentialsMissing(_logger, workItemId, project.Id);
            return;
        }

        var token = await _secretResolver.ResolveAsync(project.AdoTokenSecretUri, ct);

        var commentId = await _adoClient.PostCommentAsync(
            project.AdoOrgUrl!, project.AdoProjectName!, workItemIntId, token, commentBody, ct);

        if (commentId is null)
            LogCommentPostWarning(_logger, workItemId, "ADO", 0, "Post returned null");
    }

    // ─── Structured log messages ──────────────────────────────────────────────

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "FeedbackLoop: project {ProjectId} not found for comment {CommentId} — skipping")]
    private static partial void LogProjectNotFound(ILogger logger, string projectId, string commentId);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "FeedbackLoop: no prior TestRun found for work item {WorkItemId} in project {ProjectId} (comment {CommentId}) — no feedback stored")]
    private static partial void LogNoRunFound(ILogger logger, string workItemId, string projectId, string commentId);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "FeedbackLoop: last TestRun for work item {WorkItemId} in project {ProjectId} has no resolved test types (comment {CommentId}) — no feedback stored")]
    private static partial void LogNoTestTypes(ILogger logger, string workItemId, string projectId, string commentId);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "FeedbackLoop: embedding failed for work item {WorkItemId} / comment {CommentId}: {ExceptionMessage}")]
    private static partial void LogEmbeddingFailed(ILogger logger, string workItemId, string commentId, string exceptionMessage, Exception ex);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "FeedbackLoop: upserted feedback entry for work item {WorkItemId} / comment {CommentId} / testType {TestType}")]
    private static partial void LogUpserted(ILogger logger, string workItemId, string commentId, string testType);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "FeedbackLoop: upsert failed for work item {WorkItemId} / comment {CommentId} / testType {TestType}: {ExceptionMessage}")]
    private static partial void LogUpsertFailed(ILogger logger, string workItemId, string commentId, string testType, string exceptionMessage, Exception ex);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "FeedbackLoop: {PmTool} confirmation comment post failed for work item {WorkItemId} / comment {CommentId}: {ExceptionMessage}")]
    private static partial void LogConfirmationPostFailed(ILogger logger, string workItemId, string commentId, string pmTool, string exceptionMessage);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "FeedbackLoop: {PmTool} comment post warning for {WorkItemId}: HTTP {StatusCode} — {ErrorDetail}")]
    private static partial void LogCommentPostWarning(ILogger logger, string workItemId, string pmTool, int statusCode, string errorDetail);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "FeedbackLoop: Jira credentials not configured for {IssueKey} in project {ProjectId} — confirmation skipped")]
    private static partial void LogJiraCredentialsMissing(ILogger logger, string issueKey, string projectId);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "FeedbackLoop: ADO work item ID could not be parsed for '{WorkItemId}' in project {ProjectId} — confirmation skipped")]
    private static partial void LogAdoWorkItemIdMissing(ILogger logger, string workItemId, string projectId);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "FeedbackLoop: ADO token not configured for {WorkItemId} in project {ProjectId} — confirmation skipped")]
    private static partial void LogAdoCredentialsMissing(ILogger logger, string workItemId, string projectId);
}
