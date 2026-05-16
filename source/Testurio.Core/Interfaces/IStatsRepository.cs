using Testurio.Core.Entities;
using Testurio.Core.Models;

namespace Testurio.Core.Interfaces;

/// <summary>
/// Provides dashboard snapshot data: project summaries sorted server-side and quota usage.
/// </summary>
public interface IStatsRepository
{
    /// <summary>
    /// Returns all active (non-deleted) projects for <paramref name="userId"/> combined with
    /// their latest run summary, sorted server-side:
    /// <list type="bullet">
    ///   <item>Projects that have at least one run appear first, sorted by <c>latestRun.startedAt</c> descending.</item>
    ///   <item>Projects with no runs appear last, sorted alphabetically by name.</item>
    /// </list>
    /// </summary>
    Task<IReadOnlyList<DashboardProjectSummary>> GetDashboardSummariesAsync(
        string userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the quota usage for <paramref name="userId"/> for the current calendar day (UTC).
    /// <para>
    /// <c>DailyLimit</c> is 0 when the user has no active subscription plan.
    /// <c>ResetsAt</c> is always the next midnight UTC relative to when the request is processed.
    /// </para>
    /// </summary>
    Task<QuotaUsage> GetQuotaUsageAsync(
        string userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the run history list and 90-day trend points for the given project.
    /// <para>
    /// Validates that <paramref name="projectId"/> belongs to <paramref name="userId"/> before
    /// querying the <c>TestResults</c> container.
    /// Returns <c>null</c> when the project does not exist or is not owned by the user.
    /// </para>
    /// <para>
    /// Runs are returned sorted by <c>createdAt</c> descending.
    /// Trend points cover the 90 UTC days ending today (inclusive), zero-filled for days with no runs.
    /// </para>
    /// </summary>
    Task<(IReadOnlyList<RunHistoryItem> Runs, IReadOnlyList<TrendPoint> TrendPoints)?> GetProjectHistoryAsync(
        string userId,
        string projectId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the full <see cref="TestResult"/> document for a single run.
    /// <para>
    /// Validates project ownership before returning the document.
    /// Returns <c>null</c> when the run does not exist, the project does not match, or the user does not own the project.
    /// </para>
    /// </summary>
    Task<TestResult?> GetRunDetailAsync(
        string userId,
        string projectId,
        string runId,
        CancellationToken cancellationToken = default);
}
