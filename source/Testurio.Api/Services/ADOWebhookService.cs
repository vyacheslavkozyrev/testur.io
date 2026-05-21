using Microsoft.Extensions.Logging;
using Testurio.Core.Entities;
using Testurio.Core.Enums;
using Testurio.Core.Exceptions;
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
    private readonly IPlanEnforcementService _planEnforcementService;
    private readonly IADOClient _adoClient;
    private readonly ISecretResolver _secretResolver;
    private readonly ILogger<ADOWebhookService> _logger;

    public ADOWebhookService(
        ITestRunRepository testRunRepository,
        IRunQueueRepository runQueueRepository,
        ITestRunJobSender jobSender,
        IWorkItemTypeFilterService filterService,
        IPlanEnforcementService planEnforcementService,
        IADOClient adoClient,
        ISecretResolver secretResolver,
        ILogger<ADOWebhookService> logger)
    {
        _testRunRepository = testRunRepository;
        _runQueueRepository = runQueueRepository;
        _jobSender = jobSender;
        _filterService = filterService;
        _planEnforcementService = planEnforcementService;
        _adoClient = adoClient;
        _secretResolver = secretResolver;
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

    private async Task<WebhookProcessResult?> CheckQuotaAsync(
        Project project,
        string workItemId,
        CancellationToken cancellationToken)
    {
        try
        {
            await _planEnforcementService.CheckMonthlyRunQuotaAsync(project.UserId, cancellationToken);
            return null;
        }
        catch (PlanLimitExceededException ex)
        {
            await PostQuotaCommentAsync(project, workItemId, ex.Message, cancellationToken);
            LogQuotaExceeded(_logger, workItemId, project.Id, ex.LimitName);
            return WebhookProcessResult.QuotaExceeded;
        }
    }

    private async Task PostQuotaCommentAsync(
        Project project,
        string workItemId,
        string message,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(project.AdoOrgUrl) ||
            string.IsNullOrEmpty(project.AdoProjectName) ||
            string.IsNullOrEmpty(project.AdoTokenSecretRef))
            return;

        if (!int.TryParse(workItemId, out var workItemIdInt))
            return;

        var token = await _secretResolver.ResolveAsync(project.AdoTokenSecretRef, cancellationToken);
        var result = await _adoClient.PostCommentAsync(
            project.AdoOrgUrl, project.AdoProjectName, workItemIdInt, token, message, cancellationToken);
        if (result is null)
            LogQuotaCommentPostFailed(_logger, workItemId, project.Id);
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
        Message = "Plan limit exceeded for ADO work item {WorkItemId} in project {ProjectId}: limit '{LimitName}'")]
    private static partial void LogQuotaExceeded(ILogger logger, string workItemId, string projectId, string limitName);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Failed to post quota comment on ADO work item {WorkItemId} in project {ProjectId}")]
    private static partial void LogQuotaCommentPostFailed(ILogger logger, string workItemId, string projectId);
}
