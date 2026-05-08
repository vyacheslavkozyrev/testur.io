using Testurio.Core.Entities;

namespace Testurio.Core.Repositories;

public interface ITestRunRepository
{
    Task<TestRun?> GetByIdAsync(string projectId, string id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TestRun>> GetByProjectAsync(string projectId, int limit = 50, CancellationToken cancellationToken = default);
    Task<TestRun> CreateAsync(TestRun testRun, CancellationToken cancellationToken = default);
    Task<TestRun> UpdateAsync(TestRun testRun, CancellationToken cancellationToken = default);
}
