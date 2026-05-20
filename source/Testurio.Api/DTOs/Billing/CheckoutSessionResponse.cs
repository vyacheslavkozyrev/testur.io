namespace Testurio.Api.DTOs.Billing;

/// <summary>
/// Response body for <c>POST /v1/billing/checkout</c>.
/// The client should immediately redirect to <see cref="CheckoutUrl"/>.
/// </summary>
public sealed record CheckoutSessionResponse(string CheckoutUrl);
