using Testurio.Core.Entities;

namespace Testurio.Core.Repositories;

public interface IRunQueueRepository
{
    Task<IReadOnlyList<QueuedRun>> GetQueueAsync(string projectId, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(string projectId, string jiraIssueId, CancellationToken cancellationToken = default);
    Task<QueuedRun> EnqueueAsync(QueuedRun queuedRun, CancellationToken cancellationToken = default);
    Task<QueuedRun?> DequeueNextAsync(string projectId, CancellationToken cancellationToken = default);
    Task DeleteAsync(string projectId, string id, CancellationToken cancellationToken = default);
}
