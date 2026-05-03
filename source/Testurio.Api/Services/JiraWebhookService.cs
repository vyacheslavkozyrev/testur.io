using Microsoft.Extensions.Logging;
using Testurio.Core.Entities;
using Testurio.Core.Interfaces;
using Testurio.Core.Models;
using Testurio.Core.Repositories;

namespace Testurio.Api.Services;

public partial class JiraWebhookService : IJiraWebhookService
{
    private const string UserStoryIssueType = "Story";
    private const string SkipReasonIncompleteStory = "Skipped — incomplete story";

    private readonly ITestRunRepository _testRunRepository;
    private readonly IRunQueueRepository _runQueueRepository;
    private readonly ITestRunJobSender _jobSender;
    private readonly IJiraApiClient _jiraApiClient;
    private readonly ISecretResolver _secretResolver;
    private readonly ILogger<JiraWebhookService> _logger;

    public JiraWebhookService(
        ITestRunRepository testRunRepository,
        IRunQueueRepository runQueueRepository,
        ITestRunJobSender jobSender,
        IJiraApiClient jiraApiClient,
        ISecretResolver secretResolver,
        ILogger<JiraWebhookService> logger)
    {
        _testRunRepository = testRunRepository;
        _runQueueRepository = runQueueRepository;
        _jobSender = jobSender;
        _jiraApiClient = jiraApiClient;
        _secretResolver = secretResolver;
        _logger = logger;
    }

    public async Task<WebhookProcessResult> ProcessAsync(
        Project project,
        JiraWebhookPayload payload,
        CancellationToken cancellationToken = default)
    {
        if (payload.WebhookEvent != "jira:issue_transitioned" || payload.Issue is null)
            return WebhookProcessResult.Ignored;

        var issue = payload.Issue;
        var fields = issue.Fields;

        if (fields?.IssueType?.Name != UserStoryIssueType)
            return WebhookProcessResult.Ignored;

        var transitionedTo = payload.Transition?.To?.Name ?? string.Empty;
        if (!string.Equals(transitionedTo, project.InTestingStatusLabel, StringComparison.OrdinalIgnoreCase))
            return WebhookProcessResult.Ignored;

        var missingParts = GetMissingParts(fields);
        if (missingParts is not null)
        {
            await HandleIncompleteStoryAsync(project, issue, missingParts, cancellationToken);
            return WebhookProcessResult.Skipped;
        }

        return await EnqueueOrQueueRunAsync(project, issue, cancellationToken);
    }

    private static string? GetMissingParts(JiraIssueFields? fields)
    {
        bool missingDescription = string.IsNullOrWhiteSpace(fields?.Description);
        bool missingAc = string.IsNullOrWhiteSpace(fields?.AcceptanceCriteria);

        if (missingDescription && missingAc)
            return "description and acceptance criteria";
        if (missingDescription)
            return "description";
        if (missingAc)
            return "acceptance criteria";
        return null;
    }

    private async Task HandleIncompleteStoryAsync(
        Project project,
        JiraIssue issue,
        string missingParts,
        CancellationToken cancellationToken)
    {
        var testRun = new TestRun
        {
            ProjectId = project.Id,
            UserId = project.UserId,
            JiraIssueKey = issue.Key,
            JiraIssueId = issue.Id,
            Status = TestRunStatus.Skipped,
            SkipReason = SkipReasonIncompleteStory
        };
        await _testRunRepository.CreateAsync(testRun, cancellationToken);

        var apiToken = await _secretResolver.ResolveAsync(project.JiraApiTokenSecretRef, cancellationToken);
        var comment = $"Testurio skipped this test run because the story is missing: {missingParts}. Please update the story and move it back to \"In Testing\" to trigger a new run.";
        var posted = await _jiraApiClient.PostCommentAsync(
            project.JiraBaseUrl, issue.Key, project.JiraEmail, apiToken, comment, cancellationToken);
        if (!posted)
            LogCommentPostFailed(_logger, issue.Key, project.Id);

        LogSkipped(_logger, issue.Key, project.Id, missingParts);
    }

    private async Task<WebhookProcessResult> EnqueueOrQueueRunAsync(
        Project project,
        JiraIssue issue,
        CancellationToken cancellationToken)
    {
        // TOCTOU race: concurrent webhooks for the same story could both pass GetActiveRunAsync.
        // A unique constraint on (ProjectId, JiraIssueId, Status=Pending) in the TestRuns Cosmos container
        // would prevent duplicate documents — configure this in infra/modules/cosmos.bicep before relying on it.
        var activeRun = await _testRunRepository.GetActiveRunAsync(project.Id, cancellationToken);
        if (activeRun is not null)
        {
            var alreadyQueued = await _runQueueRepository.ExistsAsync(project.Id, issue.Id, cancellationToken);
            if (alreadyQueued)
            {
                LogDuplicate(_logger, issue.Key, project.Id);
                return WebhookProcessResult.Queued;
            }

            var queued = new QueuedRun
            {
                ProjectId = project.Id,
                UserId = project.UserId,
                JiraIssueKey = issue.Key,
                JiraIssueId = issue.Id
            };
            await _runQueueRepository.EnqueueAsync(queued, cancellationToken);
            LogQueued(_logger, issue.Key, project.Id);
            return WebhookProcessResult.Queued;
        }

        var testRun = new TestRun
        {
            ProjectId = project.Id,
            UserId = project.UserId,
            JiraIssueKey = issue.Key,
            JiraIssueId = issue.Id,
            Status = TestRunStatus.Pending
        };
        var created = await _testRunRepository.CreateAsync(testRun, cancellationToken);

        await _jobSender.SendAsync(new TestRunJobMessage
        {
            TestRunId = created.Id,
            ProjectId = project.Id,
            UserId = project.UserId,
            JiraIssueKey = issue.Key,
            JiraIssueId = issue.Id
        }, cancellationToken);

        LogEnqueued(_logger, issue.Key, project.Id, created.Id);
        return WebhookProcessResult.Enqueued;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to post skip comment on Jira issue {IssueKey} in project {ProjectId}")]
    private static partial void LogCommentPostFailed(ILogger logger, string issueKey, string projectId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Skipped test run for {IssueKey} in project {ProjectId}: missing {MissingParts}")]
    private static partial void LogSkipped(ILogger logger, string issueKey, string projectId, string missingParts);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Duplicate webhook ignored for {IssueKey} in project {ProjectId}")]
    private static partial void LogDuplicate(ILogger logger, string issueKey, string projectId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Queued {IssueKey} in project {ProjectId} (active run in progress)")]
    private static partial void LogQueued(ILogger logger, string issueKey, string projectId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Enqueued test run {TestRunId} for {IssueKey} in project {ProjectId}")]
    private static partial void LogEnqueued(ILogger logger, string issueKey, string projectId, string testRunId);
}
