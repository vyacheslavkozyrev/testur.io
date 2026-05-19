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
        string? customerEmail,
        string userId,
        string successUrl,
        string cancelUrl,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a Stripe Subscription object by its ID, with payment method details expanded.
    /// Used by <c>BillingService.GetSubscriptionStatusAsync</c> to populate live billing data.
    /// </summary>
    /// <param name="stripeSubscriptionId">The Stripe subscription ID (<c>sub_*</c>).</param>
    /// <param name="cancellationToken">Propagated to the Stripe HTTP call.</param>
    /// <returns>The subscription entity, or <c>null</c> if not found.</returns>
    Task<UserSubscription?> GetSubscriptionAsync(
        string stripeSubscriptionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the most recent invoices for the given Stripe customer (up to <paramref name="limit"/>).
    /// Returns an empty list if the customer has no invoices or the Stripe call fails.
    /// </summary>
    /// <param name="stripeCustomerId">The Stripe customer ID (<c>cus_*</c>).</param>
    /// <param name="limit">Maximum number of invoices to return (1–100).</param>
    /// <param name="cancellationToken">Propagated to the Stripe HTTP call.</param>
    Task<IReadOnlyList<StripeInvoice>> ListInvoicesAsync(
        string stripeCustomerId,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a Stripe Customer Portal session for the given customer.
    /// </summary>
    /// <param name="stripeCustomerId">The Stripe customer ID (<c>cus_*</c>).</param>
    /// <param name="returnUrl">URL to redirect the user back to after they leave the portal.</param>
    /// <param name="cancellationToken">Propagated to the Stripe HTTP call.</param>
    /// <returns>The Stripe Customer Portal session URL.</returns>
    Task<string> CreatePortalSessionAsync(
        string stripeCustomerId,
        string returnUrl,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reactivates a cancelled subscription by setting <c>cancel_at_period_end = false</c>.
    /// </summary>
    /// <param name="stripeSubscriptionId">The Stripe subscription ID (<c>sub_*</c>).</param>
    /// <param name="cancellationToken">Propagated to the Stripe HTTP call.</param>
    Task ReactivateSubscriptionAsync(
        string stripeSubscriptionId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Lightweight DTO carrying invoice data retrieved from Stripe.
/// Keeps <c>Stripe.*</c> types out of the domain layer.
/// </summary>
public sealed record StripeInvoice(
    DateTimeOffset Date,
    decimal AmountPaid,
    string Currency,
    string Status,
    string? PdfUrl);
