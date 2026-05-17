using Testurio.Core.Models;

namespace Testurio.Api.DTOs;

/// <summary>Response body for <c>GET /v1/stats/projects/{projectId}/history</c>.</summary>
public record ProjectHistoryResponse(
    IReadOnlyList<RunHistoryItem> Runs,
    IReadOnlyList<TrendPoint> TrendPoints);
