namespace Testurio.Core.Models;

/// <summary>
/// Boolean capability flags for a subscription plan tier.
/// Each flag gates a specific pipeline or integration feature.
/// </summary>
public sealed record PlanFeatures
{
    /// <summary>Whether the plan includes API test generation and execution.</summary>
    public required bool ApiTesting { get; init; }

    /// <summary>Whether the plan includes UI end-to-end test generation and Playwright execution.</summary>
    public required bool UiE2eTesting { get; init; }

    /// <summary>Whether the plan includes AI memory (Stage 3 MemoryRetrieval and Stage 8 MemoryWriter).</summary>
    public required bool AiMemory { get; init; }

    /// <summary>Whether the plan includes posting test report comments back to the originating ADO / Jira ticket.</summary>
    public required bool PmReportPostBack { get; init; }
}
