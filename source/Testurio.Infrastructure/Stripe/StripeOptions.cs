using System.ComponentModel.DataAnnotations;

namespace Testurio.Infrastructure.Stripe;

/// <summary>
/// Configuration for the Stripe integration (non-secret fields only).
/// Bound from <c>Stripe</c> configuration section; validated at startup.
/// Secret fields (<c>SecretKey</c>, <c>WebhookSecret</c>) are sourced from
/// <c>StripeSecrets</c> which is loaded from Key Vault at startup.
/// </summary>
public class StripeOptions
{
    /// <summary>
    /// Maps plan+interval combinations to Stripe Price IDs.
    /// Keys follow the pattern <c>{plan}_{interval}</c>, e.g. <c>TestPro_Monthly</c>.
    /// </summary>
    [Required]
    public required Dictionary<string, string> PriceIds { get; init; }
}
