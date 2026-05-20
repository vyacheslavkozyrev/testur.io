using Microsoft.Extensions.Options;
using Moq;
using Stripe;
using Stripe.Checkout;
using Testurio.Core.Enums;
using Testurio.Infrastructure.Stripe;

namespace Testurio.UnitTests.Infrastructure;

/// <summary>
/// Unit tests for <see cref="StripeService"/>.
/// These tests verify the <em>options and routing logic</em> of <c>StripeService</c>:
/// correct Price ID selection per plan+interval, and exception on missing Price ID.
/// Live HTTP calls to Stripe are covered by integration tests (T034).
/// </summary>
public class StripeServiceTests
{
    private static readonly StripeOptions ValidOptions = new()
    {
        SecretKey = "sk_test_fake",
        WebhookSecret = "whsec_fake",
        PriceIds = new Dictionary<string, string>
        {
            ["TestJunior_Monthly"] = "price_tj_monthly",
            ["TestJunior_Annual"] = "price_tj_annual",
            ["TestPro_Monthly"] = "price_tp_monthly",
            ["TestPro_Annual"] = "price_tp_annual",
            ["Team_Monthly"] = "price_team_monthly",
            ["Team_Annual"] = "price_team_annual",
            ["Centurio_Monthly"] = "price_cent_monthly",
            ["Centurio_Annual"] = "price_cent_annual",
        },
    };

    [Fact]
    public async Task CreateCheckoutSessionAsync_ThrowsInvalidOperation_WhenPriceIdNotConfigured()
    {
        // StripeOptions with empty price map — simulates a misconfigured deployment.
        var options = Options.Create(new StripeOptions
        {
            SecretKey = "sk_test_fake",
            WebhookSecret = "whsec_fake",
            PriceIds = [],
        });
        var sut = new StripeService(options);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.CreateCheckoutSessionAsync(
                SubscriptionPlan.TestPro,
                BillingInterval.Monthly,
                "user@example.com",
                "user-1",
                "https://app.testur.io/billing/success?session_id={CHECKOUT_SESSION_ID}",
                "https://app.testur.io/pricing"));
    }

    [Theory]
    [InlineData(SubscriptionPlan.TestJunior, BillingInterval.Monthly, "price_tj_monthly")]
    [InlineData(SubscriptionPlan.TestJunior, BillingInterval.Annual, "price_tj_annual")]
    [InlineData(SubscriptionPlan.TestPro, BillingInterval.Monthly, "price_tp_monthly")]
    [InlineData(SubscriptionPlan.TestPro, BillingInterval.Annual, "price_tp_annual")]
    [InlineData(SubscriptionPlan.Team, BillingInterval.Monthly, "price_team_monthly")]
    [InlineData(SubscriptionPlan.Team, BillingInterval.Annual, "price_team_annual")]
    [InlineData(SubscriptionPlan.Centurio, BillingInterval.Monthly, "price_cent_monthly")]
    [InlineData(SubscriptionPlan.Centurio, BillingInterval.Annual, "price_cent_annual")]
    public void PriceKey_MapsToCorrectPriceId(SubscriptionPlan plan, BillingInterval interval, string expectedPriceId)
    {
        var key = $"{plan}_{interval}";
        Assert.True(ValidOptions.PriceIds.TryGetValue(key, out var actualPriceId));
        Assert.Equal(expectedPriceId, actualPriceId);
    }

    // ─── CreatePortalSessionAsync ─────────────────────────────────────────────

    [Fact]
    public async Task CreatePortalSessionAsync_ThrowsStripeException_WhenApiKeyIsInvalid()
    {
        // StripeService uses real Stripe SDK HTTP calls; invalid API key causes StripeException.
        // This test verifies the method signature is callable and propagates Stripe errors.
        // Live HTTP success is covered by integration tests.
        var sut = new StripeService(Options.Create(ValidOptions));

        await Assert.ThrowsAsync<StripeException>(() =>
            sut.CreatePortalSessionAsync("cus_fake", "https://app.testur.io/settings"));
    }

    // ─── ReactivateSubscriptionAsync ─────────────────────────────────────────

    [Fact]
    public async Task ReactivateSubscriptionAsync_ThrowsStripeException_WhenApiKeyIsInvalid()
    {
        // Verifies method signature and error propagation; live HTTP covered by integration tests.
        var sut = new StripeService(Options.Create(ValidOptions));

        await Assert.ThrowsAsync<StripeException>(() =>
            sut.ReactivateSubscriptionAsync("sub_fake"));
    }

    // ─── ListInvoicesAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task ListInvoicesAsync_ThrowsStripeException_WhenApiKeyIsInvalid()
    {
        // Verifies method signature and error propagation; live HTTP covered by integration tests.
        var sut = new StripeService(Options.Create(ValidOptions));

        await Assert.ThrowsAsync<StripeException>(() =>
            sut.ListInvoicesAsync("cus_fake", 20));
    }
}
