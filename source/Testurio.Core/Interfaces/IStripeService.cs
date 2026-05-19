using Testurio.Core.Enums;
using Testurio.Core.Entities;

namespace Testurio.Core.Interfaces;

/// <summary>
/// Abstraction over the Stripe API used by <c>BillingService</c>.
/// The concrete implementation lives in <c>Testurio.Infrastructure</c>.
/// </summary>
public interface IStripeService
{
    /// <summary>
    /// Creates a Stripe Checkout Session with a 14-day trial, pre-populated customer email,
    /// and the correct Price ID for the given plan and billing interval.
    /// </summary>
    /// <param name="plan">The subscription plan tier selected by the user.</param>
    /// <param name="billingInterval">Monthly or annual billing.</param>
    /// <param name="customerEmail">User's email address used to pre-populate the Stripe Checkout form.</param>
    /// <param name="successUrl">URL Stripe redirects to after successful checkout (must include <c>{CHECKOUT_SESSION_ID}</c>).</param>
    /// <param name="cancelUrl">URL Stripe redirects to when the user cancels checkout.</param>
    /// <param name="cancellationToken">Propagated to the Stripe HTTP call.</param>
    /// <returns>The Stripe Checkout Session URL to redirect the user to.</returns>
    Task<string> CreateCheckoutSessionAsync(
        SubscriptionPlan plan,
        BillingInterval billingInterval,
        string customerEmail,
        string userId,
        string successUrl,
        string cancelUrl,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a Stripe Subscription object by its ID.
    /// Reserved for future use (e.g., feature 0016 — subscription management).
    /// Not called by <c>BillingService</c> in the current feature; webhook events are used instead.
    /// </summary>
    /// <param name="stripeSubscriptionId">The Stripe subscription ID (<c>sub_*</c>).</param>
    /// <param name="cancellationToken">Propagated to the Stripe HTTP call.</param>
    /// <returns>The subscription entity, or <c>null</c> if not found.</returns>
    Task<UserSubscription?> GetSubscriptionAsync(
        string stripeSubscriptionId,
        CancellationToken cancellationToken = default);
}
