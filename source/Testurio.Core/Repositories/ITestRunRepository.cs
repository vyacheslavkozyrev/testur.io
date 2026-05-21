using Testurio.Core.Entities;

namespace Testurio.Core.Repositories;

public interface ITestRunRepository
{
    Task<TestRun?> GetByIdAsync(string projectId, string id, CancellationToken cancellationToken = default);
    Task<TestRun?> GetActiveRunAsync(string projectId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TestRun>> GetByProjectAsync(string projectId, int limit = 50, CancellationToken cancellationToken = default);
    Task<TestRun> CreateAsync(TestRun testRun, CancellationToken cancellationToken = default);
    Task<TestRun> UpdateAsync(TestRun testRun, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the most recent <see cref="TestRun"/> for the given <paramref name="workItemId"/> and
    /// <paramref name="projectId"/>, ordered by <c>createdAt</c> descending, or <c>null</c> when no
    /// prior run exists. Used by FeedbackLoop (stage 7, feature 0031) to resolve
    /// <c>resolvedTestTypes</c> for feedback memory entries (AC-007 / AC-008).
    /// </summary>
    Task<TestRun?> GetMostRecentByWorkItemAsync(string projectId, string workItemId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the count of <see cref="TestRun"/> documents for <paramref name="userId"/>
    /// whose <c>createdAt</c> falls within [<paramref name="periodStart"/>, <paramref name="periodEnd"/>).
    /// Used by plan enforcement for both calendar-month quota (paid plans) and 14-day trial-period
    /// quota (trialing users). Cross-partition fan-out — only called on the webhook/trigger path.
    /// </summary>
    Task<int> CountByUserForMonthAsync(
        string userId,
        DateTimeOffset periodStart,
        DateTimeOffset periodEnd,
        CancellationToken cancellationToken = default);
}
