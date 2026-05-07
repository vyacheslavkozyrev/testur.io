using Testurio.Core.Entities;

namespace Testurio.Core.Repositories;

public interface ITestScenarioRepository
{
    Task<IReadOnlyList<TestScenario>> GetByRunAsync(string projectId, string testRunId, CancellationToken cancellationToken = default);
    Task<TestScenario> CreateAsync(TestScenario scenario, CancellationToken cancellationToken = default);
    Task CreateBatchAsync(IEnumerable<TestScenario> scenarios, CancellationToken cancellationToken = default);
}
