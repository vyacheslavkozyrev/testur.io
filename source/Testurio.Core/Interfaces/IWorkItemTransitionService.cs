using Testurio.Core.Entities;
using Testurio.Core.Enums;

namespace Testurio.Core.Interfaces;

/// <summary>
/// Result returned by <see cref="IWorkItemTransitionService.TransitionAsync"/>.
/// </summary>
public sealed record WorkItemTransitionResult(
    StatusTransitionOutcome Outcome,
    string? TransitionedTo,
    string? ErrorDetail);

/// <summary>
/// Service that performs a post-run PM tool work item status transition (feature 0024).
/// Resolves the PM tool type and credentials from the project document, then calls the
/// appropriate client (<see cref="IJiraClient"/> or <see cref="IADOClient"/>).
/// Never throws — errors are captured in the returned <see cref="WorkItemTransitionResult"/>.
/// </summary>
public interface IWorkItemTransitionService
{
    /// <summary>
    /// Attempts to transition the originating PM tool work item to <paramref name="targetStatusName"/>.
    /// Returns <see cref="StatusTransitionOutcome.NotConfigured"/> immediately when
    /// <paramref name="targetStatusName"/> is null or whitespace.
    /// </summary>
    Task<WorkItemTransitionResult> TransitionAsync(
        Project project,
        TestRun testRun,
        string? targetStatusName,
        CancellationToken cancellationToken = default);
}
