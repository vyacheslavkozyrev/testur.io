namespace Testurio.Core.Enums;

/// <summary>
/// Lifecycle state of a user's subscription.
/// </summary>
public enum SubscriptionStatus
{
    /// <summary>The user has never started a trial or purchased a plan.</summary>
    None,

    /// <summary>The user is within an active free trial period.</summary>
    Trialing,

    /// <summary>The user has an active paid subscription.</summary>
    Active,

    /// <summary>The trial or subscription has ended without renewal.</summary>
    Expired
}
