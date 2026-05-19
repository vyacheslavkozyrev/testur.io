using Testurio.Core.Enums;

namespace Testurio.Core.Entities;

/// <summary>
/// Persisted subscription record for a user, stored in the <c>UserSubscriptions</c> Cosmos DB container.
/// Partition key: <see cref="UserId"/>.
/// </summary>
public class UserSubscription
{
    /// <summary>Document ID — same as <see cref="UserId"/> (one subscription per user in v1).</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Azure AD B2C OID — partition key.</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>The selected subscription plan tier.</summary>
    public SubscriptionPlan Plan { get; set; }

    /// <summary>Whether the user is billed monthly or annually.</summary>
    public BillingInterval BillingInterval { get; set; }

    /// <summary>Current lifecycle state of the subscription.</summary>
    public SubscriptionStatus Status { get; set; } = SubscriptionStatus.None;

    /// <summary>
    /// UTC timestamp when the 14-day free trial ends.
    /// <c>null</c> when status is <see cref="SubscriptionStatus.None"/> or <see cref="SubscriptionStatus.Active"/>.
    /// </summary>
    public DateTimeOffset? TrialEndsAt { get; set; }

    /// <summary>Stripe Customer ID (<c>cus_*</c>), populated after the first checkout session completes.</summary>
    public string? StripeCustomerId { get; set; }

    /// <summary>Stripe Subscription ID (<c>sub_*</c>), populated after the first checkout session completes.</summary>
    public string? StripeSubscriptionId { get; set; }

    /// <summary>UTC timestamp when this document was last written.</summary>
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
