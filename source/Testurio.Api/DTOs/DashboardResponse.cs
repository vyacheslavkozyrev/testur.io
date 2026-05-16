using Testurio.Core.Models;

namespace Testurio.Api.DTOs;

/// <summary>Response body for <c>GET /v1/stats/dashboard</c>.</summary>
public record DashboardResponse(
    IReadOnlyList<DashboardProjectSummary> Projects,
    QuotaUsage QuotaUsage);
