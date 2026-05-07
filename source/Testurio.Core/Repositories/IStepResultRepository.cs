using Testurio.Core.Entities;

namespace Testurio.Core.Repositories;

public interface IStepResultRepository
{
    Task<IReadOnlyList<StepResult>> GetByRunAsync(string projectId, string testRunId, CancellationToken cancellationToken = default);
    Task CreateBatchAsync(IEnumerable<StepResult> results, CancellationToken cancellationToken = default);
}
