using Testurio.Core.Enums;
using Testurio.Core.Exceptions;
using Testurio.Core.Interfaces;
using Testurio.Core.Models;
using Testurio.Core.Repositories;

namespace Testurio.Infrastructure.Enforcement;

/// <summary>
/// Centralises plan-limit enforcement for both <c>Testurio.Api</c> and <c>Testurio.Worker</c>.
/// Resolves the user's effective plan from <see cref="IUserSubscriptionRepository"/> +
/// <see cref="IPlanRepository"/> and checks limits and feature flags.
/// </summary>
public sealed class PlanEnforcementService : IPlanEnforcementService
{
    /// <summary>
    /// Display names of the next upgrade tier, keyed by the current tier.
    /// Used to populate <c>PlanLimitExceededException.RequiredPlan</c>.
    /// </summary>
    private static readonly IReadOnlyDictionary<SubscriptionPlan, string> NextTierNames =
        new Dictionary<SubscriptionPlan, string>
        {
            [SubscriptionPlan.TestJunior] = "Test Pro",
            [SubscriptionPlan.TestPro] = "Team",
            [SubscriptionPlan.Team] = "Centurio",
            [SubscriptionPlan.Centurio] = "Centurio", // already at highest tier
        };

    private readonly IUserSubscriptionRepository _subscriptionRepository;
    private readonly IPlanRepository _planRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly ITestRunRepository _testRunRepository;

    public PlanEnforcementService(
        IUserSubscriptionRepository subscriptionRepository,
        IPlanRepository planRepository,
        IProjectRepository projectRepository,
        ITestRunRepository testRunRepository)
    {
        _subscriptionRepository = subscriptionRepository;
        _planRepository = planRepository;
        _projectRepository = projectRepository;
        _testRunRepository = testRunRepository;
    }

    /// <inheritdoc />
    public async Task<PlanDocument?> GetEffectivePlanAsync(string userId, CancellationToken ct = default)
    {
        var subscription = await _subscriptionRepository.GetByUserIdAsync(userId, ct);
        if (subscription is null)
            return null;

        if (subscription.Status is not (SubscriptionStatus.Active or SubscriptionStatus.Trialing or SubscriptionStatus.CancelledPendingExpiry))
            return null;

        return await _planRepository.GetByPlanAsync(subscription.Plan, ct);
    }

    /// <inheritdoc />
    public async Task CheckProjectLimitAsync(string userId, CancellationToken ct = default)
    {
        var subscription = await _subscriptionRepository.GetByUserIdAsync(userId, ct);
        var utcNow = DateTimeOffset.UtcNow;

        // ── Trial path ────────────────────────────────────────────────────────
        if (subscription?.Status == SubscriptionStatus.Trialing)
        {
            if (subscription.TrialEndsAt is null || subscription.TrialEndsAt.Value <= utcNow)
            {
                // Expired trial — block project creation entirely.
                throw new PlanLimitExceededException(
                    "Your trial has ended. Purchase a plan to create more projects.",
                    limitName: "maxProjects",
                    requiredPlan: "Test Junior");
            }

            // Active trial — enforce fixed 2-project cap regardless of plan tier.
            var projects = await _projectRepository.ListByUserAsync(userId, ct);
            if (projects.Count >= 2)
            {
                throw new PlanLimitExceededException(
                    "Your trial allows a maximum of 2 projects. Purchase a plan to create more.",
                    limitName: "maxProjects",
                    requiredPlan: "Test Junior");
            }

            return;
        }

        // ── Paid / non-trialing path ──────────────────────────────────────────
        var plan = await GetEffectivePlanAsync(userId, ct);
        if (plan is null)
        {
            // No active plan — treat as a finite limit of 0 (always exceeded).
            throw new PlanLimitExceededException(
                "Your plan allows a maximum of 0 projects. Upgrade to Test Junior to create more.",
                limitName: "maxProjects",
                requiredPlan: "Test Junior");
        }

        var maxProjects = plan.Limits.MaxProjects;
        if (maxProjects == -1)
            return; // unlimited — skip check

        var existingProjects = await _projectRepository.ListByUserAsync(userId, ct);
        var activeCount = existingProjects.Count; // ListByUserAsync already filters deleted projects

        if (activeCount >= maxProjects)
        {
            var requiredPlan = TryGetPlanEnum(plan.Id, out var planEnum) && NextTierNames.TryGetValue(planEnum, out var next)
                ? next
                : plan.Name;
            throw new PlanLimitExceededException(
                $"Your plan allows a maximum of {maxProjects} projects. Upgrade to {requiredPlan} to create more.",
                limitName: "maxProjects",
                requiredPlan: requiredPlan);
        }
    }

    /// <inheritdoc />
    public async Task CheckMonthlyRunQuotaAsync(string userId, CancellationToken ct = default)
    {
        var subscription = await _subscriptionRepository.GetByUserIdAsync(userId, ct);
        var utcNow = DateTimeOffset.UtcNow;

        // ── Trial path ────────────────────────────────────────────────────────
        if (subscription?.Status == SubscriptionStatus.Trialing)
        {
            if (subscription.TrialEndsAt is null || subscription.TrialEndsAt.Value <= utcNow)
            {
                // Expired trial — 0-run limit.
                throw new PlanLimitExceededException(
                    "Your trial has ended. Purchase a plan to run more tests.",
                    limitName: "maxTestRunsPerMonth",
                    requiredPlan: "Test Junior");
            }

            // Active trial — count runs in the 14-day trial window and apply plan-tier limit.
            var trialEndsAt = subscription.TrialEndsAt.Value;
            var trialStart = trialEndsAt.AddDays(-14);

            var planDoc = await _planRepository.GetByPlanAsync(subscription.Plan, ct);
            var maxRuns = planDoc?.Limits.MaxTestRunsPerMonth ?? 0;

            if (maxRuns != -1) // -1 = unlimited
            {
                var usedInTrial = await _testRunRepository.CountByUserForMonthAsync(userId, trialStart, trialEndsAt, ct);
                if (usedInTrial >= maxRuns)
                {
                    throw new PlanLimitExceededException(
                        $"Your trial allows {maxRuns} test runs. Trial ends on {trialEndsAt:yyyy-MM-dd}.",
                        limitName: "maxTestRunsPerMonth",
                        requiredPlan: "Test Junior");
                }
            }

            return;
        }

        // ── Paid / non-trialing path ──────────────────────────────────────────
        var plan = await GetEffectivePlanAsync(userId, ct);
        if (plan is null)
        {
            // No active plan — treat as a finite limit of 0 (always exceeded).
            var now = DateTimeOffset.UtcNow;
            var nextMonth = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero).AddMonths(1);
            throw new PlanLimitExceededException(
                $"Your plan allows 0 test runs per month. Your quota resets on {nextMonth:yyyy-MM-dd}. Upgrade to Test Junior to run more tests.",
                limitName: "maxTestRunsPerMonth",
                requiredPlan: "Test Junior");
        }

        var planMaxRuns = plan.Limits.MaxTestRunsPerMonth;
        if (planMaxRuns == -1)
            return; // unlimited — skip check

        // Count TestRun documents created this calendar month for this user.
        // Cross-partition query is acceptable here — enforcement fires only on webhook/trigger path.
        var nowEnforcement = DateTimeOffset.UtcNow;
        var monthStart = new DateTimeOffset(nowEnforcement.Year, nowEnforcement.Month, 1, 0, 0, 0, TimeSpan.Zero);
        var nextMonthStart = monthStart.AddMonths(1);

        var usedThisMonth = await _testRunRepository.CountByUserForMonthAsync(userId, monthStart, nextMonthStart, ct);

        if (usedThisMonth >= planMaxRuns)
        {
            var requiredPlan = TryGetPlanEnum(plan.Id, out var planEnum) && NextTierNames.TryGetValue(planEnum, out var next)
                ? next
                : plan.Name;
            throw new PlanLimitExceededException(
                $"Your plan allows {planMaxRuns} test runs per month. Your quota resets on {nextMonthStart:yyyy-MM-dd}. Upgrade to {requiredPlan} to run more tests.",
                limitName: "maxTestRunsPerMonth",
                requiredPlan: requiredPlan);
        }
    }

    // ─── Private helpers ──────────────────────────────────────────────────────

    private static bool TryGetPlanEnum(string planId, out SubscriptionPlan plan)
    {
        switch (planId)
        {
            case "test-junior": plan = SubscriptionPlan.TestJunior; return true;
            case "test-pro": plan = SubscriptionPlan.TestPro; return true;
            case "team": plan = SubscriptionPlan.Team; return true;
            case "centurio": plan = SubscriptionPlan.Centurio; return true;
            default: plan = default; return false;
        }
    }
}
