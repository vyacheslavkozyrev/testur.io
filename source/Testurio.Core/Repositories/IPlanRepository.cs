using Testurio.Core.Models;

namespace Testurio.Core.Repositories;

public interface IPlanRepository
{
    Task<IReadOnlyList<PlanDocument>> ListAllAsync(CancellationToken cancellationToken = default);
}
