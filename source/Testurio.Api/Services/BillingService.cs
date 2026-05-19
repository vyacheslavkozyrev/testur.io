using Microsoft.Extensions.Options;
using Stripe;
using Testurio.Api.DTOs.Billing;
using Testurio.Api.Options;
using Testurio.Core.Entities;
using Testurio.Core.Enums;
using Testurio.Core.Interfaces;
using Testurio.Core.Repositories;
using Testurio.Infrastructure.Stripe;

namespace Testurio.Api.Services;

public interface IBillingService
{
    Task<CheckoutSessionResponse> CreateCheckoutSessionAsync(
        string userId,
        string userEmail,
        CreateCheckoutSessionRequest request,
        CancellationToken cancellationToken = default);

    Task<SubscriptionStatusResponse> GetSubscriptionStatusAsync(
        string userId,
        CancellationToken cancellationToken = default);

    Task HandleStripeWebhookAsync(
        string payload,
        string stripeSignature,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Handles all billing operations: Stripe Checkout session creation,
/// subscription status queries, and Stripe webhook processing.
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
            return new SubscriptionStatusResponse(SubscriptionStatus.None, null, null, null);

        return new SubscriptionStatusResponse(
            subscription.Status,
            subscription.Plan,
            subscription.BillingInterval,
            subscription.TrialEndsAt);
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

        // Find user by Stripe customer ID
        var customerId = stripeSubscription.CustomerId;
        if (string.IsNullOrEmpty(customerId))
            return;

        // Map Stripe status to domain status
        var status = stripeSubscription.Status switch
        {
            "trialing" => SubscriptionStatus.Trialing,
            "active"   => SubscriptionStatus.Active,
            _          => SubscriptionStatus.Expired,
        };

        // Retrieve the subscription document by iterating metadata (client_reference_id is on session, not subscription)
        // In this implementation we use the Stripe subscription metadata userId field set at checkout
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

        existing.Status = status;
        existing.TrialEndsAt = stripeSubscription.TrialEnd;
        existing.UpdatedAt = DateTimeOffset.UtcNow;

        await subscriptionRepository.UpsertAsync(existing, cancellationToken);

        logger.LogInformation(
            "Subscription updated for user {UserId} — new status: {Status}.",
            userId, status);
    }
}
