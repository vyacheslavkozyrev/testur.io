using Testurio.Core.Exceptions;
using Testurio.Core.Models;

namespace Testurio.Core.Interfaces;

/// <summary>
/// Centralises plan-limit enforcement so that both <c>Testurio.Api</c> and <c>Testurio.Worker</c>
/// resolve a user's effective plan in a single, consistent service call.
/// </summary>
public interface IPlanEnforcementService
{
    /// <summary>
    /// Returns the user's active <see cref="PlanDocument"/>, or <c>null</c> when the user has no
    /// active subscription (status is <c>None</c>, <c>Expired</c>, or the record is missing).
    /// </summary>
    Task<PlanDocument?> GetEffectivePlanAsync(string userId, CancellationToken ct = default);

    /// <summary>
    /// Checks whether the user may create a new project under their current plan.
    /// </summary>
    /// <exception cref="PlanLimitExceededException">
    /// Thrown when the user's non-deleted project count equals or exceeds
    /// <c>PlanDocument.Limits.MaxProjects</c> (and <c>MaxProjects != -1</c>).
    /// Also thrown when the user has no active plan and <c>MaxProjects</c> would be finite.
    /// </exception>
    Task CheckProjectLimitAsync(string userId, CancellationToken ct = default);

    /// <summary>
    /// Checks whether the user may trigger another test run this calendar month.
    /// </summary>
    /// <exception cref="PlanLimitExceededException">
    /// Thrown when the count of <c>TestRun</c> documents created this calendar month equals or
    /// exceeds <c>PlanDocument.Limits.MaxTestRunsPerMonth</c> (and the limit is not <c>-1</c>).
    /// Also thrown when the user has no active plan and <c>MaxTestRunsPerMonth</c> would be finite.
    /// </exception>
    Task CheckMonthlyRunQuotaAsync(string userId, CancellationToken ct = default);
}
