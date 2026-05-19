using System.ComponentModel.DataAnnotations;
using Testurio.Core.Enums;

namespace Testurio.Api.DTOs.Billing;

/// <summary>
/// Request body for <c>POST /v1/billing/checkout</c>.
/// </summary>
public sealed record CreateCheckoutSessionRequest(
    [Required] SubscriptionPlan Plan,
    [Required] BillingInterval BillingInterval);
