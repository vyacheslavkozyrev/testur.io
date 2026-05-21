using Testurio.Core.Enums;
using Testurio.Core.Models;

namespace Testurio.Core.Repositories;

public interface IPlanRepository
{
    Task<IReadOnlyList<PlanDocument>> ListAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the <see cref="PlanDocument"/> for the given <paramref name="plan"/> tier,
    /// or <c>null</c> when no matching document exists in the <c>Plans</c> container.
    /// Used by the enforcement layer to avoid fetching all plans when only one is needed.
    /// </summary>
    Task<PlanDocument?> GetByPlanAsync(SubscriptionPlan plan, CancellationToken cancellationToken = default);
}
