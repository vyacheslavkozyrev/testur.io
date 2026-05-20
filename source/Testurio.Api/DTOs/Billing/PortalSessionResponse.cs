namespace Testurio.Api.DTOs.Billing;

/// <summary>
/// Response body for <c>POST /v1/billing/portal-session</c>.
/// </summary>
public sealed record PortalSessionResponse(string PortalUrl);
