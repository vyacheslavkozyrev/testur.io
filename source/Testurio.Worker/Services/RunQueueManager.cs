using Microsoft.Extensions.Logging;
using Testurio.Core.Entities;
using Testurio.Core.Interfaces;
using Testurio.Core.Models;
using Testurio.Core.Repositories;

namespace Testurio.Worker.Services;

public partial class RunQueueManager
{
    private readonly ITestRunRepository _testRunRepository;
    private readonly IRunQueueRepository _runQueueRepository;
    private readonly ITestRunJobSender _jobSender;
    private readonly ILogger<RunQueueManager> _logger;

    public RunQueueManager(
        ITestRunRepository testRunRepository,
        IRunQueueRepository runQueueRepository,
        ITestRunJobSender jobSender,
        ILogger<RunQueueManager> logger)
    {
        _testRunRepository = testRunRepository;
        _runQueueRepository = runQueueRepository;
        _jobSender = jobSender;
        _logger = logger;
    }

    public async Task OnRunCompletedAsync(string projectId, CancellationToken cancellationToken = default)
    {
        var next = await _runQueueRepository.DequeueNextAsync(projectId, cancellationToken);
        if (next is null)
        {
            LogQueueEmpty(_logger, projectId);
            return;
        }

        await _runQueueRepository.DeleteAsync(projectId, next.Id, cancellationToken);

        var testRun = new TestRun
        {
            ProjectId = projectId,
            UserId = next.UserId,
            JiraIssueKey = next.JiraIssueKey,
            JiraIssueId = next.JiraIssueId,
            Status = TestRunStatus.Pending
        };
        var created = await _testRunRepository.CreateAsync(testRun, cancellationToken);

        await _jobSender.SendAsync(new TestRunJobMessage
        {
            TestRunId = created.Id,
            ProjectId = projectId,
            UserId = next.UserId,
            JiraIssueKey = next.JiraIssueKey,
            JiraIssueId = next.JiraIssueId
        }, cancellationToken);

        LogDispatchedNext(_logger, created.Id, next.JiraIssueKey, projectId);
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "No queued runs for project {ProjectId}")]
    private static partial void LogQueueEmpty(ILogger logger, string projectId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Dispatched queued run {TestRunId} for {IssueKey} in project {ProjectId}")]
    private static partial void LogDispatchedNext(ILogger logger, string testRunId, string issueKey, string projectId);
}
