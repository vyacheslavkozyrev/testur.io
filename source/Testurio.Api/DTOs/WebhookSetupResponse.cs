namespace Testurio.Api.DTOs;

/// <summary>
/// Response body for GET /v1/projects/{projectId}/integrations/webhook-setup.
/// Shows the webhook URL and masked/plaintext secret state.
/// </summary>
public sealed record WebhookSetupResponse(
    /// <summary>The public Testurio webhook endpoint URL to register in the PM tool.</summary>
    string WebhookUrl,

    /// <summary>
    /// The webhook secret in plaintext on first view; masked ("••••••••") on subsequent views.
    /// </summary>
    string WebhookSecret,

    /// <summary>True if the secret has already been viewed once and is now masked.</summary>
    bool IsMasked);
