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
}
