using Testurio.Api.DTOs;
using Testurio.Core.Interfaces;

namespace Testurio.Api.Services;

public interface IDashboardService
{
    Task<DashboardResponse> GetDashboardAsync(string userId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Assembles the dashboard snapshot response by calling <see cref="IStatsRepository"/>.
/// Sorting is performed server-side inside the repository; this service only orchestrates the two calls.
/// </summary>
public class DashboardService : IDashboardService
{
    private readonly IStatsRepository _statsRepository;

    public DashboardService(IStatsRepository statsRepository)
    {
        _statsRepository = statsRepository;
    }

    public async Task<DashboardResponse> GetDashboardAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        var summaries = await _statsRepository.GetDashboardSummariesAsync(userId, cancellationToken);
        var quota = await _statsRepository.GetQuotaUsageAsync(userId, cancellationToken);
        return new DashboardResponse(summaries, quota);
    }
}
