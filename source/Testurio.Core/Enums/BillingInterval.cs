namespace Testurio.Core.Enums;

/// <summary>
/// Billing recurrence selected by the user at checkout.
/// </summary>
public enum BillingInterval
{
    /// <summary>Billed once per calendar month.</summary>
    Monthly,

    /// <summary>Billed once per year at an annual discount.</summary>
    Annual
}
