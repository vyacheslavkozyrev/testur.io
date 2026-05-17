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
    string? RawCommentMarkdown);
