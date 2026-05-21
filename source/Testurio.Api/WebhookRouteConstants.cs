namespace Testurio.Api;

internal static class WebhookRouteConstants
{
    internal const string JiraPrefix = "/v1/webhooks/jira";
    /// <summary>
    /// All webhook paths — both the legacy <c>/v1/webhooks</c> family and <c>/webhooks</c>
    /// (Stripe, feature 0015) — need body buffering so handlers can re-read the raw payload
    /// for signature validation.
    /// </summary>
    internal const string BufferingPathPrefix = "/webhooks";
    /// <summary>
    /// Versioned webhook prefix for Jira and ADO webhook handlers under <c>/v1/webhooks</c>.
    /// </summary>
    internal const string V1BufferingPathPrefix = "/v1/webhooks";
}
