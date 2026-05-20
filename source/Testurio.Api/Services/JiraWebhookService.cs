using Microsoft.Extensions.Logging;
using Testurio.Core.Entities;
using Testurio.Core.Enums;
using Testurio.Core.Interfaces;
using Testurio.Core.Models;
using Testurio.Core.Repositories;

namespace Testurio.Api.Services;

public partial class JiraWebhookService : IJiraWebhookService
{
    private const string SkipReasonIncompleteStory = "Skipped — incomplete story";

    private readonly ITestRunRepository _testRunRepository;
    private readonly IRunQueueRepository _runQueueRepository;
    private readonly ITestRunJobSender _jobSender;
    private readonly IJiraApiClient _jiraApiClient;
    private readonly ISecretResolver _secretResolver;
    private readonly IWorkItemTypeFilterService _filterService;
    private readonly IQuotaPolicy _quotaPolicy;
    private readonly IUserSubscriptionRepository _subscriptionRepository;
    private readonly ILogger<JiraWebhookService> _logger;

    public JiraWebhookService(
        ITestRunRepository testRunRepository,
        IRunQueueRepository runQueueRepository,
        ITestRunJobSender jobSender,
        IJiraApiClient jiraApiClient,
        ISecretResolver secretResolver,
        IWorkItemTypeFilterService filterService,
        IQuotaPolicy quotaPolicy,
        IUserSubscriptionRepository subscriptionRepository,
        ILogger<JiraWebhookService> logger)
    {
        _testRunRepository = testRunRepository;
        _runQueueRepository = runQueueRepository;
        _jobSender = jobSender;
        _jiraApiClient = jiraApiClient;
        _secretResolver = secretResolver;
        _filterService = filterService;
        _quotaPolicy = quotaPolicy;
        _subscriptionRepository = subscriptionRepository;
        _logger = logger;
    }

    public async Task<WebhookProcessResult> ProcessAsync(
        Project project,
        JiraWebhookPayload payload,
        CancellationToken cancellationToken = default)
    {
        if (payload.WebhookEvent != "jira:issue_updated" || payload.Issue is null)
            return WebhookProcessResult.Ignored;

        var issue = payload.Issue;
        var fields = issue.Fields;

        var issueType = fields?.IssueType?.Name ?? string.Empty;
        if (!_filterService.IsAllowed(project, issueType))
        {
            LogFiltered(_logger, issueType, project.Id, "webhook_filtered", "issue_type_not_allowed");
            return WebhookProcessResult.Ignored;
        }

        var statusChange = payload.Changelog?.Items.FirstOrDefault(i => i.Field == "status");
        if (statusChange is null)
            return WebhookProcessResult.Ignored;

        var transitionedTo = statusChange.ToString ?? string.Empty;
        if (!string.Equals(transitionedTo, project.InTestingStatusLabel, StringComparison.OrdinalIgnoreCase))
            return WebhookProcessResult.Ignored;

        var quotaResult = await CheckQuotaAsync(project, issue, cancellationToken);
        if (quotaResult is not null)
            return quotaResult.Value;

        var missingParts = GetMissingParts(fields);
        if (missingParts is not null)
        {
            await HandleIncompleteStoryAsync(project, issue, missingParts, cancellationToken);
            return WebhookProcessResult.Skipped;
        }

        return await EnqueueOrQueueRunAsync(project, issue, cancellationToken);
    }

    private async Task<WebhookProcessResult?> CheckQuotaAsync(
        Project project,
        JiraIssue issue,
        CancellationToken cancellationToken)
    {
        var subscription = await _subscriptionRepository.GetByUserIdAsync(project.UserId, cancellationToken);

        var isActiveSubscription = subscription is not null &&
            subscription.Status is SubscriptionStatus.Trialing or SubscriptionStatus.Active;

        var dailyLimit = isActiveSubscription
            ? _quotaPolicy.GetDailyLimit(subscription!.Plan)
            : 0;

        // Users with no subscription always get limit 0 regardless of quota policy.
        if (!isActiveSubscription || dailyLimit == 0)
        {
            await PostQuotaCommentAsync(project, issue, noSubscription: true, usedToday: 0, dailyLimit: 0, cancellationToken);
            LogQuotaRejectedNoSubscription(_logger, issue.Key, project.Id);
            return WebhookProcessResult.QuotaExceeded;
        }

        var now = DateTimeOffset.UtcNow;
        var windowStart = new DateTimeOffset(now.Year, now.Month, now.Day, 0, 0, 0, TimeSpan.Zero);
        var windowEnd = windowStart.AddDays(1);

        var usedToday = await _testRunRepository.CountTodayAsync(project.UserId, windowStart, windowEnd, cancellationToken);
        if (usedToday >= dailyLimit)
        {
            await PostQuotaCommentAsync(project, issue, noSubscription: false, usedToday, dailyLimit, cancellationToken);
            LogQuotaExceeded(_logger, issue.Key, project.Id, usedToday, dailyLimit);
            return WebhookProcessResult.QuotaExceeded;
        }

        return null;
    }

    private async Task PostQuotaCommentAsync(
        Project project,
        JiraIssue issue,
        bool noSubscription,
        int usedToday,
        int dailyLimit,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(project.JiraApiTokenSecretRef) ||
            string.IsNullOrEmpty(project.JiraBaseUrl) ||
            string.IsNullOrEmpty(project.JiraEmail))
        {
            return;
        }

        var apiToken = await _secretResolver.ResolveAsync(project.JiraApiTokenSecretRef, cancellationToken);

        string comment;
        if (noSubscription)
        {
            comment = "Testurio could not start a test run because this project does not have an active subscription. " +
                      "Please purchase or renew a plan at https://testur.io to enable automated testing.";
        }
        else
        {
            var resetsAt = DateTimeOffset.UtcNow.Date.AddDays(1);
            comment = $"Testurio could not start a test run because the daily quota has been reached " +
                      $"({usedToday}/{dailyLimit} runs used today). " +
                      $"The quota resets at {resetsAt:HH:mm} UTC.";
        }

        var posted = await _jiraApiClient.PostCommentAsync(
            project.JiraBaseUrl, issue.Key, project.JiraEmail, apiToken, comment, cancellationToken);
        if (!posted.IsSuccess)
            LogQuotaCommentPostFailed(_logger, issue.Key, project.Id);
    }

    private static string? GetMissingParts(JiraIssueFields? fields)
    {
        if (string.IsNullOrWhiteSpace(fields?.Description))
            return "description";
        return null;
    }

    private async Task HandleIncompleteStoryAsync(
        Project project,
        JiraIssue issue,
        string missingParts,
        CancellationToken cancellationToken)
    {
        // Resolve the secret before writing the TestRun — a Key Vault failure leaves no orphaned record
        // and Jira will re-deliver the webhook so the operation can be retried.
        var apiToken = await _secretResolver.ResolveAsync(project.JiraApiTokenSecretRef!, cancellationToken);

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
        var comment = $"Testurio skipped this test run because the story is missing: {missingParts}. Please update the story and move it back to \"In Testing\" to trigger a new run.";
        var posted = await _jiraApiClient.PostCommentAsync(
            project.JiraBaseUrl!, issue.Key, project.JiraEmail!, apiToken, comment, cancellationToken);
        if (!posted.IsSuccess)
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

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Webhook filtered: issue type '{IssueType}' is not in the allowed list for project {ProjectId}; {EventType} {Reason}")]
    private static partial void LogFiltered(ILogger logger, string issueType, string projectId, string eventType, string reason);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Quota exceeded for Jira issue {IssueKey} in project {ProjectId}: {UsedToday}/{DailyLimit} runs used today")]
    private static partial void LogQuotaExceeded(ILogger logger, string issueKey, string projectId, int usedToday, int dailyLimit);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Quota rejected (no active subscription) for Jira issue {IssueKey} in project {ProjectId}")]
    private static partial void LogQuotaRejectedNoSubscription(ILogger logger, string issueKey, string projectId);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Failed to post quota comment on Jira issue {IssueKey} in project {ProjectId}")]
    private static partial void LogQuotaCommentPostFailed(ILogger logger, string issueKey, string projectId);
}
