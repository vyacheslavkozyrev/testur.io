using Microsoft.Extensions.Logging;
using Testurio.Core.Enums;
using Testurio.Core.Interfaces;

namespace Testurio.Infrastructure.Quota;

/// <summary>
/// Maps each <see cref="SubscriptionPlan"/> tier to its daily test-run quota limit.
/// Returns <c>0</c> when no plan is provided (no active subscription).
/// </summary>
public sealed partial class QuotaPolicy : IQuotaPolicy
{
    private readonly ILogger<QuotaPolicy> _logger;

    public QuotaPolicy(ILogger<QuotaPolicy> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public int GetDailyLimit(SubscriptionPlan? plan)
    {
        switch (plan)
        {
            case SubscriptionPlan.TestJunior: return 10;
            case SubscriptionPlan.TestPro: return 30;
            case SubscriptionPlan.Team: return 100;
            case SubscriptionPlan.Centurio: return 500;
            case null: return 0;
            default:
                LogUnrecognisedPlan(_logger, plan.Value);
                return 0;
        }
    }

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "QuotaPolicy encountered an unrecognised SubscriptionPlan value '{Plan}'; returning 0. Update QuotaPolicy when new plan tiers are added.")]
    private static partial void LogUnrecognisedPlan(ILogger logger, SubscriptionPlan plan);
}
