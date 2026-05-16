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
}
