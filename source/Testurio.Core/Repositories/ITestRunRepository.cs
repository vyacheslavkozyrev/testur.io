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
    /// Counts all <see cref="TestRun"/> documents for <paramref name="userId"/> whose
    /// <c>createdAt</c> timestamp falls within <c>[<paramref name="windowStart"/>, <paramref name="windowEnd"/>)</c>.
    /// All statuses, including <c>Skipped</c>, are included.
    /// This is a cross-partition fan-out query; it is only called on the webhook processing path,
    /// not on the hot read path.
    /// </summary>
    Task<int> CountTodayAsync(string userId, DateTimeOffset windowStart, DateTimeOffset windowEnd, CancellationToken cancellationToken = default);
}
