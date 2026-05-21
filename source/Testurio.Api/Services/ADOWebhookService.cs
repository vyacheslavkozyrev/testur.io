using Microsoft.Extensions.Logging;
using Testurio.Core.Entities;
using Testurio.Core.Enums;
using Testurio.Core.Interfaces;
using Testurio.Core.Models;
using Testurio.Core.Repositories;

namespace Testurio.Api.Services;

public interface IADOWebhookService
{
    Task<WebhookProcessResult> ProcessAsync(Project project, ADOWebhookPayload payload, CancellationToken cancellationToken = default);
}

public partial class ADOWebhookService : IADOWebhookService
{
    private readonly ITestRunRepository _testRunRepository;
    private readonly IRunQueueRepository _runQueueRepository;
    private readonly ITestRunJobSender _jobSender;
    private readonly IWorkItemTypeFilterService _filterService;
    private readonly IQuotaPolicy _quotaPolicy;
    private readonly IUserSubscriptionRepository _subscriptionRepository;
    private readonly ILogger<ADOWebhookService> _logger;

    public ADOWebhookService(
        ITestRunRepository testRunRepository,
        IRunQueueRepository runQueueRepository,
        ITestRunJobSender jobSender,
        IWorkItemTypeFilterService filterService,
        IQuotaPolicy quotaPolicy,
        IUserSubscriptionRepository subscriptionRepository,
        ILogger<ADOWebhookService> logger)
    {
        _testRunRepository = testRunRepository;
        _runQueueRepository = runQueueRepository;
        _jobSender = jobSender;
        _filterService = filterService;
        _quotaPolicy = quotaPolicy;
        _subscriptionRepository = subscriptionRepository;
        _logger = logger;
    }

    public async Task<WebhookProcessResult> ProcessAsync(
        Project project,
        ADOWebhookPayload payload,
        CancellationToken cancellationToken = default)
    {
        if (payload.EventType != "workitem.updated" || payload.Resource is null)
            return WebhookProcessResult.Ignored;

        var stateChange = payload.Resource.Fields?.State;
        if (stateChange is null)
            return WebhookProcessResult.Ignored;

        var transitionedTo = stateChange.NewValue ?? string.Empty;
        if (!string.Equals(transitionedTo, project.AdoInTestingStatus, StringComparison.OrdinalIgnoreCase))
            return WebhookProcessResult.Ignored;

        var workItemType = payload.Resource.Revision?.Fields?.WorkItemType ?? string.Empty;
        if (!_filterService.IsAllowed(project, workItemType))
        {
            LogFiltered(_logger, workItemType, project.Id, "webhook_filtered", "issue_type_not_allowed");
            return WebhookProcessResult.Ignored;
        }

        var workItemId = payload.Resource.WorkItemId.ToString();

        // Quota check — AC-021: same logic as JiraWebhookService; no PM tool comment for ADO (AC-022).
        var quotaResult = await CheckQuotaAsync(project, workItemId, cancellationToken);
        if (quotaResult is not null)
            return quotaResult.Value;

        var activeRun = await _testRunRepository.GetActiveRunAsync(project.Id, cancellationToken);
        if (activeRun is not null)
        {
            var alreadyQueued = await _runQueueRepository.ExistsAsync(project.Id, workItemId, cancellationToken);
            if (alreadyQueued)
            {
                LogDuplicate(_logger, workItemId, project.Id);
                return WebhookProcessResult.Queued;
            }

            // JiraIssueKey/JiraIssueId are reused for ADO work item IDs until the domain entity
            // is extended with ADO-specific fields (tracked as a future improvement).
            var queued = new QueuedRun
            {
                ProjectId = project.Id,
                UserId = project.UserId,
                JiraIssueKey = workItemId,
                JiraIssueId = workItemId,
            };
            await _runQueueRepository.EnqueueAsync(queued, cancellationToken);
            LogQueued(_logger, workItemId, project.Id);
            return WebhookProcessResult.Queued;
        }

        // JiraIssueKey/JiraIssueId are reused for ADO work item IDs until the domain entity
        // is extended with ADO-specific fields (tracked as a future improvement).
        var testRun = new TestRun
        {
            ProjectId = project.Id,
            UserId = project.UserId,
            JiraIssueKey = workItemId,
            JiraIssueId = workItemId,
            Status = TestRunStatus.Pending,
        };
        var created = await _testRunRepository.CreateAsync(testRun, cancellationToken);

        await _jobSender.SendAsync(new TestRunJobMessage
        {
            TestRunId = created.Id,
            ProjectId = project.Id,
            UserId = project.UserId,
            JiraIssueKey = workItemId,
            JiraIssueId = workItemId,
        }, cancellationToken);

        LogEnqueued(_logger, workItemId, project.Id, created.Id);
        return WebhookProcessResult.Enqueued;
    }

    /// <summary>
    /// Checks the daily quota for the given project's user.
    /// Returns <see cref="WebhookProcessResult.QuotaExceeded"/> when the quota is exhausted or
    /// no active subscription exists, or <c>null</c> when the quota allows the trigger to proceed.
    /// No PM tool comment is posted for ADO (AC-022) — rejection is silent and logged only.
    /// </summary>
    private async Task<WebhookProcessResult?> CheckQuotaAsync(
        Project project,
        string workItemId,
        CancellationToken cancellationToken)
    {
        var subscription = await _subscriptionRepository.GetByUserIdAsync(project.UserId, cancellationToken);
        var isActiveSubscription = subscription is not null &&
            subscription.Status is SubscriptionStatus.Trialing or SubscriptionStatus.Active;
        var dailyLimit = isActiveSubscription ? _quotaPolicy.GetDailyLimit(subscription!.Plan) : 0;

        if (!isActiveSubscription || dailyLimit == 0)
        {
            LogQuotaRejectedNoSubscription(_logger, workItemId, project.Id);
            return WebhookProcessResult.QuotaExceeded;
        }

        var now = DateTimeOffset.UtcNow;
        var windowStart = new DateTimeOffset(now.Year, now.Month, now.Day, 0, 0, 0, TimeSpan.Zero);
        var windowEnd = windowStart.AddDays(1);
        var usedToday = await _testRunRepository.CountTodayAsync(project.UserId, windowStart, windowEnd, cancellationToken);
        if (usedToday >= dailyLimit)
        {
            LogQuotaExceeded(_logger, workItemId, project.Id, usedToday, dailyLimit);
            return WebhookProcessResult.QuotaExceeded;
        }

        return null;
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "ADO webhook filtered: work item type '{WorkItemType}' is not in the allowed list for project {ProjectId}; {EventType} {Reason}")]
    private static partial void LogFiltered(ILogger logger, string workItemType, string projectId, string eventType, string reason);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Duplicate ADO webhook ignored for work item {WorkItemId} in project {ProjectId}")]
    private static partial void LogDuplicate(ILogger logger, string workItemId, string projectId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Queued ADO work item {WorkItemId} in project {ProjectId} (active run in progress)")]
    private static partial void LogQueued(ILogger logger, string workItemId, string projectId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Enqueued test run {TestRunId} for ADO work item {WorkItemId} in project {ProjectId}")]
    private static partial void LogEnqueued(ILogger logger, string workItemId, string projectId, string testRunId);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Quota exceeded for ADO work item {WorkItemId} in project {ProjectId}: {UsedToday}/{DailyLimit} runs used today")]
    private static partial void LogQuotaExceeded(ILogger logger, string workItemId, string projectId, int usedToday, int dailyLimit);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Quota rejected (no active subscription) for ADO work item {WorkItemId} in project {ProjectId}")]
    private static partial void LogQuotaRejectedNoSubscription(ILogger logger, string workItemId, string projectId);
}
