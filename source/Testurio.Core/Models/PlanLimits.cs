namespace Testurio.Core.Models;

/// <summary>
/// Enforcement ceilings for a subscription plan tier.
/// A value of <c>-1</c> means unlimited (no ceiling is applied).
/// </summary>
public sealed record PlanLimits
{
    /// <summary>
    /// Maximum number of non-deleted projects the user may own simultaneously.
    /// <c>-1</c> = unlimited.
    /// </summary>
    public required int MaxProjects { get; init; }

    /// <summary>
    /// Maximum number of test runs the user may trigger in a single calendar month (UTC).
    /// <c>-1</c> = unlimited.
    /// </summary>
    public required int MaxTestRunsPerMonth { get; init; }
}
