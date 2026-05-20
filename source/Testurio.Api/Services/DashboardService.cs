using Testurio.Api.DTOs;
using Testurio.Core.Enums;
using Testurio.Core.Interfaces;
using Testurio.Core.Repositories;

namespace Testurio.Api.Services;

public interface IDashboardService
{
    Task<DashboardResponse> GetDashboardAsync(string userId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Assembles the dashboard snapshot response by calling <see cref="IStatsRepository"/>.
/// Resolves the user's subscription and daily limit via <see cref="IQuotaPolicy"/> before
/// calling <see cref="IStatsRepository.GetQuotaUsageAsync"/> so the repository does not
/// need to know about subscription plans.
/// Sorting is performed server-side inside the repository; this service only orchestrates the calls.
/// </summary>
public class DashboardService : IDashboardService
{
    private readonly IStatsRepository _statsRepository;
    private readonly IQuotaPolicy _quotaPolicy;
    private readonly IUserSubscriptionRepository _subscriptionRepository;

    public DashboardService(
        IStatsRepository statsRepository,
        IQuotaPolicy quotaPolicy,
        IUserSubscriptionRepository subscriptionRepository)
    {
        _statsRepository = statsRepository;
        _quotaPolicy = quotaPolicy;
        _subscriptionRepository = subscriptionRepository;
    }

    public async Task<DashboardResponse> GetDashboardAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        // Resolve subscription to determine the correct daily limit (AC-010, AC-011, AC-013).
        var subscription = await _subscriptionRepository.GetByUserIdAsync(userId, cancellationToken);

        var isActiveSubscription = subscription is not null &&
            subscription.Status is SubscriptionStatus.Trialing or SubscriptionStatus.Active;

        var dailyLimit = isActiveSubscription
            ? _quotaPolicy.GetDailyLimit(subscription!.Plan)
            : 0;

        var summariesTask = _statsRepository.GetDashboardSummariesAsync(userId, cancellationToken);
        var quotaTask = _statsRepository.GetQuotaUsageAsync(userId, dailyLimit, cancellationToken);
        await Task.WhenAll(summariesTask, quotaTask);
        return new DashboardResponse(summariesTask.Result, quotaTask.Result);
    }
}
