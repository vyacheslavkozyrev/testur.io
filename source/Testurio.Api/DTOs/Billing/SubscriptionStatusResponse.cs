using Testurio.Core.Enums;

namespace Testurio.Api.DTOs.Billing;

/// <summary>
/// Response body for <c>GET /v1/billing/subscription</c>.
/// </summary>
public sealed record SubscriptionStatusResponse(
    SubscriptionStatus Status,
    SubscriptionPlan? Plan,
    BillingInterval? BillingInterval,
    DateTimeOffset? TrialEndsAt,
    DateTimeOffset CurrentPeriodEnd,
    DateTimeOffset? CancelledAt,
    string? PaymentMethodLast4,
    int? PaymentMethodExpMonth,
    int? PaymentMethodExpYear,
    InvoiceDto[] Invoices);
