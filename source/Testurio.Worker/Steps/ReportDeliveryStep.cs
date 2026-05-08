using Microsoft.Extensions.Logging;
using Testurio.Core.Entities;
using Testurio.Core.Enums;
using Testurio.Core.Repositories;
using Testurio.Plugins.ReportWriterPlugin;

namespace Testurio.Worker.Steps;

/// <summary>
/// Invokes <see cref="ReportWriterPlugin"/> to deliver the run report to Jira,
/// then updates the run status to Completed or ReportDeliveryFailed accordingly.
/// Logs delivery failures to Application Insights (AC-015).
/// </summary>
public partial class ReportDeliveryStep
{
    private readonly ReportWriterPlugin _reportWriterPlugin;
    private readonly ITestRunRepository _testRunRepository;
    private readonly ILogger<ReportDeliveryStep> _logger;

    public ReportDeliveryStep(
        ReportWriterPlugin reportWriterPlugin,
        ITestRunRepository testRunRepository,
        ILogger<ReportDeliveryStep> logger)
    {
        _reportWriterPlugin = reportWriterPlugin;
        _testRunRepository = testRunRepository;
        _logger = logger;
    }

    public async Task ExecuteAsync(TestRun testRun, CancellationToken cancellationToken = default)
    {
        var result = await _reportWriterPlugin.DeliverAsync(
            testRun.ProjectId,
            testRun.Id,
            cancellationToken);

        if (result.IsSuccess)
        {
            testRun.Status = TestRunStatus.Completed;
            testRun.CompletedAt = DateTimeOffset.UtcNow;
            await _testRunRepository.UpdateAsync(testRun, cancellationToken);
            LogDelivered(_logger, testRun.Id, testRun.ProjectId);
        }
        else
        {
            // Record delivery error against the run so it is visible in run history (AC-016).
            testRun.Status = TestRunStatus.ReportDeliveryFailed;
            testRun.DeliveryError = result.ErrorMessage;
            testRun.CompletedAt = DateTimeOffset.UtcNow;

            // Use CancellationToken.None — the host may be shutting down but the status write must complete.
            await _testRunRepository.UpdateAsync(testRun, CancellationToken.None);

            // Log to Application Insights for QA lead notification (AC-015).
            LogDeliveryFailed(_logger, testRun.Id, testRun.ProjectId, result.ErrorMessage ?? "unknown error");
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Report delivered successfully for run {TestRunId} in project {ProjectId}")]
    private static partial void LogDelivered(ILogger logger, string testRunId, string projectId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Report delivery failed for run {TestRunId} in project {ProjectId}: {ErrorMessage}")]
    private static partial void LogDeliveryFailed(ILogger logger, string testRunId, string projectId, string errorMessage);
}
