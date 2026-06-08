namespace Testurio.Infrastructure.Options;

/// <summary>
/// Holds Stripe secret credentials loaded from Azure Key Vault at startup.
/// Used only by <c>Testurio.Api</c> — not registered in <c>Testurio.Worker</c>.
/// Registered as a singleton — never bind from <c>IOptions&lt;T&gt;</c>.
/// </summary>
public sealed class StripeSecrets
{
    /// <summary>
    /// Stripe secret API key (<c>sk_live_*</c> or <c>sk_test_*</c>).
    /// Key Vault secret name: <c>stripe-secret-key</c>.
    /// Development config key: <c>Stripe__SecretKey</c>.
    /// </summary>
    public string SecretKey { get; init; } = string.Empty;

    /// <summary>
    /// Stripe webhook signing secret (<c>whsec_*</c>).
    /// Key Vault secret name: <c>stripe-webhook-secret</c>.
    /// Development config key: <c>Stripe__WebhookSecret</c>.
    /// </summary>
    public string WebhookSecret { get; init; } = string.Empty;
}
