using Testurio.Core.Enums;
using Testurio.Core.Models;

namespace Testurio.Api.DTOs;

/// <summary>Response body for <c>GET /v1/stats/projects/{projectId}/runs/{runId}</c>.</summary>
public record RunDetailResponse(
    string Id,
    string RunId,
    string StoryTitle,
    string Verdict,
    string Recommendation,
    long TotalDurationMs,
    DateTimeOffset CreatedAt,
    IReadOnlyList<ScenarioSummary> ScenarioResults,
    string? RawCommentMarkdown,

    // Post-run status transition outcome — feature 0024
    /// <summary>Outcome of the automatic PM tool status transition. Null when the transition step has not yet run.</summary>
    StatusTransitionOutcome? StatusTransitionOutcome,
    /// <summary>Error detail when <see cref="StatusTransitionOutcome"/> is <c>Failed</c>. Null otherwise.</summary>
    string? StatusTransitionError,
    /// <summary>The PM tool status name the work item was transitioned to. Null when not configured or failed.</summary>
    string? StatusTransitionedTo);
