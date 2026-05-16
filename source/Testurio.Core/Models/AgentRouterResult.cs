using Testurio.Core.Enums;

namespace Testurio.Core.Models;

/// <summary>
/// Output of the AgentRouter pipeline stage (stage 2).
/// Carries the resolved test types to run and Claude's rationale for the classification decision.
/// When <see cref="ResolvedTestTypes"/> is empty the router determined no applicable test type
/// exists for the story; the pipeline should mark the run as Skipped.
/// </summary>
public sealed record AgentRouterResult
{
    /// <summary>
    /// Test types that were both suggested by Claude and permitted by the project configuration.
    /// Empty when the story cannot be mapped to any configured test type.
    /// </summary>
    public required TestType[] ResolvedTestTypes { get; init; }

    /// <summary>
    /// Claude's brief rationale for the classification decision.
    /// Always populated — present on both successful and empty-result paths.
    /// </summary>
    public required string ClassificationReason { get; init; }
}
