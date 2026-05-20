namespace Testurio.Api.DTOs.Billing;

/// <summary>
/// A single invoice entry returned as part of <see cref="SubscriptionStatusResponse"/>.
/// </summary>
public sealed record InvoiceDto(
    DateTimeOffset Date,
    decimal Amount,
    string Currency,
    string Status,
    string? PdfUrl);
