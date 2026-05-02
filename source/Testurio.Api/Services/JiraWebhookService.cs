using Microsoft.Extensions.Logging;
using Testurio.Core.Entities;
using Testurio.Core.Interfaces;
using Testurio.Core.Models;
using Testurio.Core.Repositories;

namespace Testurio.Api.Services;

public partial class JiraWebhookService
{
    private const string UserStoryIssueType = "Story";
    private const string SkipReasonIncompleteStory = "Skipped — incomplete story";

    private readonly IProjectRepository _projectRepository;
    private readonly ITestRunRepository _testRunRepository;
    private readonly IRunQueueRepository _runQueueRepository;
    private readonly ITestRunJobSender _jobSender;
    private readonly IJiraApiClient _jiraApiClient;
    private readonly ILogger<JiraWebhookService> _logger;

    public JiraWebhookService(
        IProjectRepository projectRepository,
        ITestRunRepository testRunRepository,
        IRunQueueRepository runQueueRepository,
        ITestRunJobSender jobSender,
        IJiraApiClient jiraApiClient,
        ILogger<JiraWebhookService> logger)
    {
        _projectRepository = projectRepository;
        _testRunRepository = testRunRepository;
        _runQueueRepository = runQueueRepository;
        _jobSender = jobSender;
        _jiraApiClient = jiraApiClient;
        _logger = logger;
    }

    public async Task<WebhookProcessResult> ProcessAsync(
        string userId,
        string projectId,
        JiraWebhookPayload payload,
        CancellationToken cancellationToken = default)
    {
        if (payload.WebhookEvent != "jira:issue_transitioned" || payload.Issue is null)
            return WebhookProcessResult.Ignored;

        var issue = payload.Issue;
        var fields = issue.Fields;

        if (fields?.IssueType?.Name != UserStoryIssueType)
            return WebhookProcessResult.Ignored;

        var project = await _projectRepository.GetByIdAsync(userId, projectId, cancellationToken);
        if (project is null)
            return WebhookProcessResult.Ignored;

        var transitionedTo = payload.Transition?.To?.Name ?? fields.Status?.Name ?? string.Empty;
        if (!string.Equals(transitionedTo, project.InTestingStatusLabel, StringComparison.OrdinalIgnoreCase))
            return WebhookProcessResult.Ignored;

        var missingParts = GetMissingParts(fields);
        if (missingParts is not null)
        {
            await HandleIncompleteStoryAsync(userId, projectId, project, issue, missingParts, cancellationToken);
            return WebhookProcessResult.Skipped;
        }

        await EnqueueOrQueueRunAsync(userId, projectId, project, issue, cancellationToken);
        return WebhookProcessResult.Enqueued;
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
        string userId,
        string projectId,
        Project project,
        JiraIssue issue,
        string missingParts,
        CancellationToken cancellationToken)
    {
        var testRun = new TestRun
        {
            ProjectId = projectId,
            UserId = userId,
            JiraIssueKey = issue.Key,
            JiraIssueId = issue.Id,
            Status = TestRunStatus.Skipped,
            SkipReason = SkipReasonIncompleteStory
        };
        await _testRunRepository.CreateAsync(testRun, cancellationToken);

        var comment = $"Testurio skipped this test run because the story is missing: {missingParts}. Please update the story and move it back to \"In Testing\" to trigger a new run.";
        await _jiraApiClient.PostCommentAsync(
            project.JiraBaseUrl, issue.Key, project.JiraEmail, project.JiraApiToken, comment, cancellationToken);

        LogSkipped(_logger, issue.Key, projectId, missingParts);
    }

    private async Task EnqueueOrQueueRunAsync(
        string userId,
        string projectId,
        Project project,
        JiraIssue issue,
        CancellationToken cancellationToken)
    {
        var activeRun = await _testRunRepository.GetActiveRunAsync(projectId, cancellationToken);
        if (activeRun is not null)
        {
            var alreadyQueued = await _runQueueRepository.ExistsAsync(projectId, issue.Id, cancellationToken);
            if (alreadyQueued)
            {
                LogDuplicate(_logger, issue.Key, projectId);
                return;
            }

            var queued = new QueuedRun
            {
                ProjectId = projectId,
                UserId = userId,
                JiraIssueKey = issue.Key,
                JiraIssueId = issue.Id
            };
            await _runQueueRepository.EnqueueAsync(queued, cancellationToken);
            LogQueued(_logger, issue.Key, projectId);
            return;
        }

        var testRun = new TestRun
        {
            ProjectId = projectId,
            UserId = userId,
            JiraIssueKey = issue.Key,
            JiraIssueId = issue.Id,
            Status = TestRunStatus.Pending
        };
        var created = await _testRunRepository.CreateAsync(testRun, cancellationToken);

        await _jobSender.SendAsync(new TestRunJobMessage
        {
            TestRunId = created.Id,
            ProjectId = projectId,
            UserId = userId,
            JiraIssueKey = issue.Key,
            JiraIssueId = issue.Id
        }, cancellationToken);

        LogEnqueued(_logger, issue.Key, projectId, created.Id);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Skipped test run for {IssueKey} in project {ProjectId}: missing {MissingParts}")]
    private static partial void LogSkipped(ILogger logger, string issueKey, string projectId, string missingParts);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Duplicate webhook ignored for {IssueKey} in project {ProjectId}")]
    private static partial void LogDuplicate(ILogger logger, string issueKey, string projectId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Queued {IssueKey} in project {ProjectId} (active run in progress)")]
    private static partial void LogQueued(ILogger logger, string issueKey, string projectId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Enqueued test run {TestRunId} for {IssueKey} in project {ProjectId}")]
    private static partial void LogEnqueued(ILogger logger, string issueKey, string projectId, string testRunId);
}

public enum WebhookProcessResult
{
    Ignored,
    Skipped,
    Enqueued
}
