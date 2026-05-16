namespace Testurio.Core.Enums;

/// <summary>
/// Represents the lifecycle status of a test run as surfaced to the dashboard.
/// All seven values map to distinct visual treatments in the UI.
/// </summary>
public enum RunStatus
{
    Queued,
    Running,
    Passed,
    Failed,
    Cancelled,
    TimedOut,
    NeverRun,
}
