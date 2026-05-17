namespace Testurio.Core.Models;

/// <summary>
/// A single day bucket in the 90-day pass/fail trend series.
/// Computed server-side by <c>StatsRepository.GetProjectHistoryAsync</c>.
/// Days with no runs have <see cref="Passed"/> and <see cref="Failed"/> both equal to zero.
/// </summary>
/// <param name="Date">The UTC calendar date this bucket represents.</param>
/// <param name="Passed">Number of test runs that produced a <c>PASSED</c> verdict on this date.</param>
/// <param name="Failed">Number of test runs that produced a <c>FAILED</c> verdict on this date.</param>
public record TrendPoint(DateOnly Date, int Passed, int Failed);
