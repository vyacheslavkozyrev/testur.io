using Microsoft.Extensions.Options;
using Stripe;
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

    public StripeService(IOptions<StripeOptions> options)
    {
        _options = options.Value;
        // Do NOT set StripeConfiguration.ApiKey globally — it is a static shared across
        // the AppDomain and would be overwritten in multi-tenant or test scenarios.
        // Pass the API key per-request via RequestOptions instead.
        _sessionService = new SessionService();
        _subscriptionService = new SubscriptionService();
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
                options: null,
                ApiRequestOptions,
                cancellationToken);

            return MapToUserSubscription(subscription);
        }
        catch (StripeException ex) when (ex.StripeError?.Code == "resource_missing")
        {
            return null;
        }
    }

    private static UserSubscription MapToUserSubscription(global::Stripe.Subscription subscription)
    {
        var status = subscription.Status switch
        {
            "trialing" => SubscriptionStatus.Trialing,
            "active"   => SubscriptionStatus.Active,
            _          => SubscriptionStatus.Expired,
        };

        return new UserSubscription
        {
            StripeSubscriptionId = subscription.Id,
            StripeCustomerId     = subscription.CustomerId,
            Status               = status,
            TrialEndsAt          = subscription.TrialEnd,
            UpdatedAt            = DateTimeOffset.UtcNow,
        };
    }
}
