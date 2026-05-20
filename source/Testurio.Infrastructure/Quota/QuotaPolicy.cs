using Testurio.Core.Enums;
using Testurio.Core.Interfaces;

namespace Testurio.Infrastructure.Quota;

/// <summary>
/// Maps each <see cref="SubscriptionPlan"/> tier to its daily test-run quota limit.
/// Returns <c>0</c> when no plan is provided (no active subscription).
/// </summary>
public sealed class QuotaPolicy : IQuotaPolicy
{
    /// <inheritdoc/>
    public int GetDailyLimit(SubscriptionPlan? plan) => plan switch
    {
        SubscriptionPlan.TestJunior => 10,
        SubscriptionPlan.TestPro   => 30,
        SubscriptionPlan.Team      => 100,
        SubscriptionPlan.Centurio  => 500,
        null                       => 0,
        _                          => 0,
    };
}
