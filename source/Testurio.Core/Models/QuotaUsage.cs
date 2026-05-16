namespace Testurio.Core.Models;

/// <summary>
/// Represents the user's daily test run quota usage, included in the dashboard snapshot.
/// <para>
/// When <see cref="DailyLimit"/> is 0, the user has no active subscription plan.
/// The UI renders "No active plan" in this case rather than a numeric ratio.
/// </para>
/// </summary>
public record QuotaUsage(
    int UsedToday,
    int DailyLimit,
    DateTimeOffset ResetsAt);
