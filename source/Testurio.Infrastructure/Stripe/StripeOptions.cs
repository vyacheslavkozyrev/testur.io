using System.ComponentModel.DataAnnotations;

namespace Testurio.Infrastructure.Stripe;

/// <summary>
/// Configuration for the Stripe integration.
/// Bound from <c>Stripe</c> configuration section; validated at startup.
/// </summary>
public class StripeOptions
{
    /// <summary>Stripe secret key (<c>sk_live_*</c> or <c>sk_test_*</c>). Loaded from Key Vault.</summary>
    [Required]
    public required string SecretKey { get; init; }

    /// <summary>Stripe webhook signing secret (<c>whsec_*</c>). Used to verify <c>Stripe-Signature</c> headers.</summary>
    [Required]
    public required string WebhookSecret { get; init; }

    /// <summary>
    /// Maps plan+interval combinations to Stripe Price IDs.
    /// Keys follow the pattern <c>{plan}_{interval}</c>, e.g. <c>TestPro_Monthly</c>.
    /// </summary>
    [Required]
    public required Dictionary<string, string> PriceIds { get; init; }
}
