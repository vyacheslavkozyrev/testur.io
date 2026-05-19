using Microsoft.Extensions.Options;
using Stripe;
using Testurio.Api.DTOs.Billing;
using Testurio.Api.Options;
using Testurio.Core.Entities;
using Testurio.Core.Enums;
using Testurio.Core.Exceptions;
using Testurio.Core.Interfaces;
using Testurio.Core.Repositories;
using Testurio.Infrastructure.Stripe;

namespace Testurio.Api.Services;

/// <summary>
/// Handles all billing operations: Stripe Checkout session creation,
/// subscription status queries, portal session creation, reactivation,
/// and Stripe webhook processing.
/// </summary>
public class BillingService(
    IStripeService stripeService,
    IUserSubscriptionRepository subscriptionRepository,
    IOptions<StripeOptions> stripeOptions,
    IOptions<AppOptions> appOptions,
    ILogger<BillingService> logger) : IBillingService
{
    private readonly StripeOptions _stripeOptions = stripeOptions.Value;
    private readonly AppOptions _appOptions = appOptions.Value;

    public async Task<CheckoutSessionResponse> CreateCheckoutSessionAsync(
        string userId,
        string userEmail,
        CreateCheckoutSessionRequest request,
        CancellationToken cancellationToken = default)
    {
        var baseUrl = _appOptions.BaseUrl;
        var successUrl = $"{baseUrl}/billing/success?session_id={{CHECKOUT_SESSION_ID}}";
        var cancelUrl = $"{baseUrl}/pricing";

        var checkoutUrl = await stripeService.CreateCheckoutSessionAsync(
            request.Plan,
            request.BillingInterval,
            userEmail,
            userId,
            successUrl,
            cancelUrl,
            cancellationToken);

        return new CheckoutSessionResponse(checkoutUrl);
    }

    public async Task<SubscriptionStatusResponse> GetSubscriptionStatusAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        var subscription = await subscriptionRepository.GetByUserIdAsync(userId, cancellationToken);

        if (subscription is null)
            return new SubscriptionStatusResponse(
                SubscriptionStatus.None, null, null, null,
                default, null, null, null, null, []);

        DateTimeOffset currentPeriodEnd = subscription.CurrentPeriodEnd;
        string? paymentMethodLast4 = subscription.PaymentMethodLast4;
        int? paymentMethodExpMonth = subscription.PaymentMethodExpMonth;
        int? paymentMethodExpYear = subscription.PaymentMethodExpYear;
        InvoiceDto[] invoices = [];

        if (!string.IsNullOrEmpty(subscription.StripeSubscriptionId))
        {
            var liveSubscription = await stripeService.GetSubscriptionAsync(
                subscription.StripeSubscriptionId, cancellationToken);

            if (liveSubscription is not null)
            {
                currentPeriodEnd = liveSubscription.CurrentPeriodEnd;
                paymentMethodLast4 = liveSubscription.PaymentMethodLast4;
                paymentMethodExpMonth = liveSubscription.PaymentMethodExpMonth;
                paymentMethodExpYear = liveSubscription.PaymentMethodExpYear;
            }
        }

        if (!string.IsNullOrEmpty(subscription.StripeCustomerId))
        {
            invoices = await FetchInvoicesAsync(subscription.StripeCustomerId, cancellationToken);
        }

        return new SubscriptionStatusResponse(
            subscription.Status,
            subscription.Plan,
            subscription.BillingInterval,
            subscription.TrialEndsAt,
            currentPeriodEnd,
            subscription.CancelledAt,
            paymentMethodLast4,
            paymentMethodExpMonth,
            paymentMethodExpYear,
            invoices);
    }

    public async Task<PortalSessionResponse> CreatePortalSessionAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        var subscription = await subscriptionRepository.GetByUserIdAsync(userId, cancellationToken);

        if (subscription?.StripeCustomerId is null)
            throw new NotFoundException("No active subscription found for the user.");

        var returnUrl = $"{_appOptions.BaseUrl}/account/settings?tab=billing";
        var portalUrl = await stripeService.CreatePortalSessionAsync(
            subscription.StripeCustomerId, returnUrl, cancellationToken);

        return new PortalSessionResponse(portalUrl);
    }

    public async Task ReactivateSubscriptionAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        var subscription = await subscriptionRepository.GetByUserIdAsync(userId, cancellationToken);

        if (subscription is null)
            throw new NotFoundException("No subscription record found for the user.");

        if (subscription.Status != SubscriptionStatus.CancelledPendingExpiry)
            throw new ConflictException(
                $"Subscription cannot be reactivated from status '{subscription.Status}'. " +
                "Only subscriptions with status 'CancelledPendingExpiry' can be reactivated.");

        if (string.IsNullOrEmpty(subscription.StripeSubscriptionId))
            throw new NotFoundException("Subscription has no associated Stripe subscription ID.");

        await stripeService.ReactivateSubscriptionAsync(subscription.StripeSubscriptionId, cancellationToken);

        subscription.Status = SubscriptionStatus.Active;
        subscription.CancelledAt = null;
        subscription.UpdatedAt = DateTimeOffset.UtcNow;

        await subscriptionRepository.UpsertAsync(subscription, cancellationToken);
    }

    public async Task HandleStripeWebhookAsync(
        string payload,
        string stripeSignature,
        CancellationToken cancellationToken = default)
    {
        Event stripeEvent;
        try
        {
            stripeEvent = EventUtility.ConstructEvent(
                payload,
                stripeSignature,
                _stripeOptions.WebhookSecret);
        }
        catch (StripeException ex)
        {
            logger.LogWarning(ex, "Stripe webhook signature validation failed.");
            throw new InvalidOperationException("Invalid Stripe webhook signature.", ex);
        }

        switch (stripeEvent.Type)
        {
            case EventTypes.CheckoutSessionCompleted:
                await HandleCheckoutSessionCompletedAsync(stripeEvent, cancellationToken);
                break;

            case EventTypes.CustomerSubscriptionUpdated:
                await HandleSubscriptionUpdatedAsync(stripeEvent, cancellationToken);
                break;

            case EventTypes.CustomerSubscriptionDeleted:
                await HandleSubscriptionDeletedAsync(stripeEvent, cancellationToken);
                break;

            case EventTypes.InvoicePaymentFailed:
                await HandleInvoicePaymentFailedAsync(stripeEvent, cancellationToken);
                break;

            default:
                logger.LogDebug("Stripe webhook event type '{EventType}' ignored.", stripeEvent.Type);
                break;
        }
    }

    private async Task HandleCheckoutSessionCompletedAsync(Event stripeEvent, CancellationToken cancellationToken)
    {
        if (stripeEvent.Data.Object is not global::Stripe.Checkout.Session session)
            return;

        var userId = session.ClientReferenceId;
        if (string.IsNullOrEmpty(userId))
        {
            logger.LogWarning("checkout.session.completed received without client_reference_id; skipping.");
            return;
        }

        var existing = await subscriptionRepository.GetByUserIdAsync(userId, cancellationToken)
            ?? new UserSubscription { Id = userId, UserId = userId };

        existing.StripeCustomerId = session.CustomerId;
        existing.StripeSubscriptionId = session.SubscriptionId;
        existing.Status = SubscriptionStatus.Trialing;
        existing.TrialEndsAt = session.Subscription?.TrialEnd;
        existing.UpdatedAt = DateTimeOffset.UtcNow;

        // Recover plan and billing interval from subscription metadata set at checkout creation.
        var metadata = session.Subscription?.Metadata ?? new Dictionary<string, string>();
        if (metadata.TryGetValue("plan", out var planStr) &&
            Enum.TryParse<SubscriptionPlan>(planStr, out var plan))
            existing.Plan = plan;

        if (metadata.TryGetValue("billingInterval", out var intervalStr) &&
            Enum.TryParse<BillingInterval>(intervalStr, out var billingInterval))
            existing.BillingInterval = billingInterval;

        await subscriptionRepository.UpsertAsync(existing, cancellationToken);

        logger.LogInformation(
            "Subscription created for user {UserId} via Stripe session {SessionId}.",
            userId, session.Id);
    }

    private async Task HandleSubscriptionUpdatedAsync(Event stripeEvent, CancellationToken cancellationToken)
    {
        if (stripeEvent.Data.Object is not global::Stripe.Subscription stripeSubscription)
            return;

        if (!stripeSubscription.Metadata.TryGetValue("userId", out var userId) || string.IsNullOrEmpty(userId))
        {
            logger.LogDebug(
                "customer.subscription.updated for Stripe subscription {SubscriptionId} has no userId metadata; skipping.",
                stripeSubscription.Id);
            return;
        }

        var existing = await subscriptionRepository.GetByUserIdAsync(userId, cancellationToken);
        if (existing is null)
        {
            logger.LogWarning(
                "No UserSubscription found for userId {UserId} from customer.subscription.updated event.",
                userId);
            return;
        }

        // Determine new status — cancellation and reactivation via portal take precedence over Stripe raw status.
        SubscriptionStatus newStatus;
        DateTimeOffset? cancelledAt = existing.CancelledAt;

        if (stripeSubscription.CancelAtPeriodEnd)
        {
            newStatus = SubscriptionStatus.CancelledPendingExpiry;
            cancelledAt = DateTimeOffset.UtcNow;
        }
        else if (existing.Status == SubscriptionStatus.CancelledPendingExpiry)
        {
            // cancel_at_period_end flipped back to false — user reactivated via portal
            newStatus = SubscriptionStatus.Active;
            cancelledAt = null;
        }
        else
        {
            newStatus = stripeSubscription.Status switch
            {
                "trialing" => SubscriptionStatus.Trialing,
                "active"   => SubscriptionStatus.Active,
                _          => SubscriptionStatus.Expired,
            };
        }

        existing.Status = newStatus;
        existing.CancelledAt = cancelledAt;
        existing.TrialEndsAt = stripeSubscription.TrialEnd;
        existing.UpdatedAt = DateTimeOffset.UtcNow;

        // Update plan and interval from metadata on every subscription.updated event (handles upgrades).
        if (stripeSubscription.Metadata.TryGetValue("plan", out var planStr) &&
            Enum.TryParse<SubscriptionPlan>(planStr, out var plan))
            existing.Plan = plan;

        if (stripeSubscription.Metadata.TryGetValue("billingInterval", out var intervalStr) &&
            Enum.TryParse<BillingInterval>(intervalStr, out var billingInterval))
            existing.BillingInterval = billingInterval;

        await subscriptionRepository.UpsertAsync(existing, cancellationToken);

        logger.LogInformation(
            "Subscription updated for user {UserId} — new status: {Status}.",
            userId, newStatus);
    }

    private async Task HandleSubscriptionDeletedAsync(Event stripeEvent, CancellationToken cancellationToken)
    {
        if (stripeEvent.Data.Object is not global::Stripe.Subscription stripeSubscription)
            return;

        var existing = await subscriptionRepository.GetByStripeSubscriptionIdAsync(
            stripeSubscription.Id, cancellationToken);

        if (existing is null)
        {
            logger.LogWarning(
                "customer.subscription.deleted received for unknown Stripe subscription {SubscriptionId}; skipping.",
                stripeSubscription.Id);
            return;
        }

        existing.Status = SubscriptionStatus.Expired;
        existing.UpdatedAt = DateTimeOffset.UtcNow;

        await subscriptionRepository.UpsertAsync(existing, cancellationToken);

        logger.LogInformation(
            "Subscription {SubscriptionId} marked Expired via customer.subscription.deleted.",
            stripeSubscription.Id);
    }

    private async Task HandleInvoicePaymentFailedAsync(Event stripeEvent, CancellationToken cancellationToken)
    {
        if (stripeEvent.Data.Object is not global::Stripe.Invoice invoice)
            return;

        if (string.IsNullOrEmpty(invoice.SubscriptionId))
        {
            logger.LogInformation(
                "invoice.payment_failed for invoice {InvoiceId} has no subscription; skipping.",
                invoice.Id);
            return;
        }

        var existing = await subscriptionRepository.GetByStripeCustomerIdAsync(
            invoice.CustomerId, cancellationToken);

        if (existing is null)
        {
            logger.LogWarning(
                "invoice.payment_failed received for unknown Stripe customer {CustomerId}; skipping.",
                invoice.CustomerId);
            return;
        }

        existing.Status = SubscriptionStatus.PaymentFailed;
        existing.UpdatedAt = DateTimeOffset.UtcNow;

        await subscriptionRepository.UpsertAsync(existing, cancellationToken);

        logger.LogInformation(
            "Subscription for customer {CustomerId} marked PaymentFailed via invoice.payment_failed.",
            invoice.CustomerId);
    }

    private async Task<InvoiceDto[]> FetchInvoicesAsync(string stripeCustomerId, CancellationToken cancellationToken)
    {
        try
        {
            var stripeInvoices = await stripeService.ListInvoicesAsync(stripeCustomerId, 20, cancellationToken);

            return stripeInvoices
                .Select(inv => new InvoiceDto(inv.Date, inv.AmountPaid, inv.Currency, inv.Status, inv.PdfUrl))
                .ToArray();
        }
        catch (StripeException ex)
        {
            logger.LogWarning(ex, "Failed to fetch invoices for customer {CustomerId}.", stripeCustomerId);
            return [];
        }
    }
}
