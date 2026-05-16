using Testurio.Core.Models;

namespace Testurio.Core.Events;

/// <summary>
/// Emitted when a project's latest run status changes.
/// Defined here so feature 0043 (<c>useDashboardStream</c>) can reference it
/// without introducing a reverse dependency on this feature.
/// <para>
/// <see cref="QuotaUsage"/> is optional — only included when the quota counter changes
/// as a result of the run (e.g. a new run starts or completes).
/// </para>
/// </summary>
public record DashboardUpdatedEvent(
    string ProjectId,
    LatestRunSummary LatestRun,
    QuotaUsage? QuotaUsage = null);
