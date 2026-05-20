namespace Testurio.Core.Enums;

/// <summary>
/// Outcome of an automatic PM tool work item status transition attempted by
/// <c>WorkItemTransitionStep</c> after report delivery (feature 0024).
/// </summary>
public enum StatusTransitionOutcome
{
    /// <summary>No transition was configured for this run's outcome — step was skipped.</summary>
    NotConfigured,

    /// <summary>The PM tool API call completed successfully and the work item was transitioned.</summary>
    Succeeded,

    /// <summary>The PM tool API call failed (network error, auth error, or non-2xx response).</summary>
    Failed
}
