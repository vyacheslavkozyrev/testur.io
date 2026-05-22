using Testurio.Core.Enums;

namespace Testurio.Core.Interfaces;

/// <summary>
/// Defines the daily test-run quota for each subscription plan tier.
/// </summary>
public interface IQuotaPolicy
{
    /// <summary>
    /// Returns the maximum number of test-run triggers allowed per calendar day (UTC)
    /// for the given subscription plan.
    /// </summary>
    /// <param name="plan">
    /// The user's active subscription plan, or <c>null</c> when no active subscription exists.
    /// </param>
    /// <returns>
    /// The daily limit for the plan tier, or <c>0</c> when <paramref name="plan"/> is <c>null</c>.
    /// </returns>
    int GetDailyLimit(SubscriptionPlan? plan);
}
