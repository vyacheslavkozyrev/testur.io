using Microsoft.Extensions.Options;
using Stripe;
using Stripe.BillingPortal;
using Stripe.Checkout;
using Testurio.Core.Entities;
using Testurio.Core.Enums;
using Testurio.Core.Interfaces;

namespace Testurio.Infrastructure.Stripe;

/// <summary>
/// Concrete Stripe API client that implements <see cref="IStripeService"/>.
/// Uses the Stripe.net SDK; API key is read from <see cref="StripeOptions"/>.
/// </summary>
public class StripeService : IStripeService
{
    private readonly StripeOptions _options;
    private readonly SessionService _sessionService;
    private readonly SubscriptionService _subscriptionService;
    private readonly Stripe.BillingPortal.SessionService _portalSessionService;
    private readonly InvoiceService _invoiceService;

    public StripeService(IOptions<StripeOptions> options)
    {
        _options = options.Value;
        // Do NOT set StripeConfiguration.ApiKey globally — it is a static shared across
        // the AppDomain and would be overwritten in multi-tenant or test scenarios.
        // Pass the API key per-request via RequestOptions instead.
        _sessionService = new SessionService();
        _subscriptionService = new SubscriptionService();
        _portalSessionService = new Stripe.BillingPortal.SessionService();
        _invoiceService = new InvoiceService();
    }

    private RequestOptions ApiRequestOptions => new() { ApiKey = _options.SecretKey };

    /// <inheritdoc/>
    public async Task<string> CreateCheckoutSessionAsync(
        SubscriptionPlan plan,
        BillingInterval billingInterval,
        string customerEmail,
        string userId,
        string successUrl,
        string cancelUrl,
        CancellationToken cancellationToken = default)
    {
        var priceKey = $"{plan}_{billingInterval}";
        if (!_options.PriceIds.TryGetValue(priceKey, out var priceId))
            throw new InvalidOperationException($"No Stripe Price ID configured for key '{priceKey}'.");

        var createOptions = new SessionCreateOptions
        {
            Mode = "subscription",
            CustomerEmail = customerEmail,
            ClientReferenceId = userId,
            LineItems =
            [
                new SessionLineItemOptions
                {
                    Price = priceId,
                    Quantity = 1,
                },
            ],
            SubscriptionData = new SessionSubscriptionDataOptions
            {
                TrialPeriodDays = 14,
                Metadata = new Dictionary<string, string>
                {
                    ["userId"]          = userId,
                    ["plan"]            = plan.ToString(),
                    ["billingInterval"] = billingInterval.ToString(),
                },
            },
            SuccessUrl = successUrl,
            CancelUrl = cancelUrl,
        };

        var session = await _sessionService.CreateAsync(createOptions, ApiRequestOptions, cancellationToken);
        return session.Url;
    }

    /// <inheritdoc/>
    public async Task<UserSubscription?> GetSubscriptionAsync(
        string stripeSubscriptionId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var subscription = await _subscriptionService.GetAsync(
                stripeSubscriptionId,
                new SubscriptionGetOptions { Expand = ["default_payment_method"] },
                ApiRequestOptions,
                cancellationToken);

            return MapToUserSubscription(subscription);
        }
        catch (StripeException ex) when (ex.StripeError?.Code == "resource_missing")
        {
            return null;
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<StripeInvoice>> ListInvoicesAsync(
        string stripeCustomerId,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var listOptions = new InvoiceListOptions
        {
            Customer = stripeCustomerId,
            Limit    = limit,
        };

        var invoices = await _invoiceService.ListAsync(listOptions, ApiRequestOptions, cancellationToken);

        return invoices.Data
            .Select(inv => new StripeInvoice(
                inv.Created,
                inv.AmountPaid / 100m,
                inv.Currency,
                inv.Status ?? string.Empty,
                inv.InvoicePdf))
            .ToList()
            .AsReadOnly();
    }

    /// <inheritdoc/>
    public async Task<string> CreatePortalSessionAsync(
        string stripeCustomerId,
        string returnUrl,
        CancellationToken cancellationToken = default)
    {
        var createOptions = new Stripe.BillingPortal.SessionCreateOptions
        {
            Customer  = stripeCustomerId,
            ReturnUrl = returnUrl,
        };

        var session = await _portalSessionService.CreateAsync(createOptions, ApiRequestOptions, cancellationToken);
        return session.Url;
    }

    /// <inheritdoc/>
    public async Task ReactivateSubscriptionAsync(
        string stripeSubscriptionId,
        CancellationToken cancellationToken = default)
    {
        var updateOptions = new SubscriptionUpdateOptions
        {
            CancelAtPeriodEnd = false,
        };

        await _subscriptionService.UpdateAsync(stripeSubscriptionId, updateOptions, ApiRequestOptions, cancellationToken);
    }

    private static UserSubscription MapToUserSubscription(global::Stripe.Subscription subscription)
    {
        var status = subscription.Status switch
        {
            "trialing" => SubscriptionStatus.Trialing,
            "active"   => SubscriptionStatus.Active,
            _          => SubscriptionStatus.Expired,
        };

        var card = subscription.DefaultPaymentMethod?.Card;

        return new UserSubscription
        {
            StripeSubscriptionId  = subscription.Id,
            StripeCustomerId      = subscription.CustomerId,
            Status                = status,
            TrialEndsAt           = subscription.TrialEnd,
            CurrentPeriodEnd      = subscription.CurrentPeriodEnd,
            PaymentMethodLast4    = card?.Last4,
            PaymentMethodExpMonth = (int?)card?.ExpMonth,
            PaymentMethodExpYear  = (int?)card?.ExpYear,
            UpdatedAt             = DateTimeOffset.UtcNow,
        };
    }
}
