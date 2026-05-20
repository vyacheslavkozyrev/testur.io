namespace Testurio.Core.Models;

/// <summary>
/// Represents the user's monthly test run quota usage, included in the dashboard snapshot.
/// <para>
/// When <see cref="MonthlyLimit"/> is 0, the user has no active subscription plan.
/// The UI renders "No active plan" in this case rather than a numeric ratio.
/// When <see cref="MonthlyLimit"/> is <c>-1</c>, the plan is unlimited; the UI renders "Unlimited".
/// </para>
/// <para>
/// <see cref="ResetsAt"/> is the first day of the next UTC calendar month (midnight UTC).
/// </para>
/// </summary>
public record QuotaUsage(
    int UsedThisMonth,
    int MonthlyLimit,
    DateTimeOffset ResetsAt);
