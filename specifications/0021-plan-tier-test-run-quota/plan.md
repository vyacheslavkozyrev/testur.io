# Implementation Plan — Plan-Tier Test Run Quota (0021)

## Overview

Feature 0046 introduced `IPlanEnforcementService` / `PlanEnforcementService` for all plan-limit
enforcement, and feature 0001 wired the webhook services to catch `PlanLimitExceededException` and
return `200 OK` with `WebhookProcessResult.QuotaExceeded`. Feature 0021's delta is **confined to
two files**: `PlanEnforcementService` (trial-period logic) and `StatsRepository` (trial-period
dashboard window). No new interfaces, no new repositories, no new middleware.

## Tasks

### Service — PlanEnforcementService

- [x] T001 [Infra] Update `PlanEnforcementService.CheckMonthlyRunQuotaAsync` with trial-period window:
  - Load `UserSubscription` directly at the top of the method (via `_subscriptionRepository`) so
    `TrialEndsAt` is available without a second DB call.
  - When `subscription?.Status == Trialing`:
    - If `TrialEndsAt <= utcNow` → throw `PlanLimitExceededException(
      "Your trial has ended. Purchase a plan to run more tests.",
      limitName: "maxTestRunsPerMonth", requiredPlan: "Test Junior")`.
    - If `TrialEndsAt > utcNow` → count runs in `[TrialEndsAt.Value.AddDays(-14), TrialEndsAt.Value)`
      via `ITestRunRepository.CountByUserForMonthAsync`; compare against
      `plan.Limits.MaxTestRunsPerMonth`; if exceeded throw `PlanLimitExceededException(
      $"Your trial allows {maxRuns} test runs. Trial ends on {TrialEndsAt:yyyy-MM-dd}.",
      limitName: "maxTestRunsPerMonth", requiredPlan: "Test Junior")`.
  - All other statuses (`Active`, `CancelledPendingExpiry`, `PaymentFailed`, `None`, `Expired`):
    existing calendar-month logic is unchanged.
  - File: `source/Testurio.Infrastructure/Enforcement/PlanEnforcementService.cs`

- [x] T002 [Infra] Update `PlanEnforcementService.CheckProjectLimitAsync` with trial 2-project cap:
  - Load `UserSubscription` at the top of the method (same pattern as T001 — avoids double-loading
    by not delegating all logic to `GetEffectivePlanAsync` when subscription is needed directly).
  - When `subscription?.Status == Trialing`:
    - If `TrialEndsAt <= utcNow` → throw `PlanLimitExceededException(
      "Your trial has ended. Purchase a plan to create more projects.",
      limitName: "maxProjects", requiredPlan: "Test Junior")`.
    - If `TrialEndsAt > utcNow` → enforce `maxProjects = 2` (ignore plan-tier `MaxProjects`);
      count existing projects via `_projectRepository.ListByUserAsync`; if `count >= 2` throw
      `PlanLimitExceededException(
      "Your trial allows a maximum of 2 projects. Purchase a plan to create more.",
      limitName: "maxProjects", requiredPlan: "Test Junior")`.
  - All other statuses: existing behaviour unchanged — uses `plan.Limits.MaxProjects`.
  - File: `source/Testurio.Infrastructure/Enforcement/PlanEnforcementService.cs`

### Infrastructure — StatsRepository

- [x] T003 [Infra] Update `StatsRepository.GetQuotaUsageAsync` with trial-period window:
  - After resolving the subscription, check `subscription?.Status == Trialing`:
    - If `TrialEndsAt <= utcNow` → return `new QuotaUsage(0, 0, TrialEndsAt.Value)`.
    - If `TrialEndsAt > utcNow` → count runs in `[TrialEndsAt.Value.AddDays(-14), TrialEndsAt.Value)`
      using the existing cross-partition `countQuery` with updated `@start` / `@end` parameters;
      resolve `monthlyLimit` from the plan as today; return
      `new QuotaUsage(usedInTrialPeriod, monthlyLimit, TrialEndsAt.Value)`.
  - All other statuses: existing calendar-month logic unchanged.
  - File: `source/Testurio.Infrastructure/Cosmos/StatsRepository.cs`

### Tests

- [x] T004 [Test] Unit tests for `PlanEnforcementService` trial paths —
  `tests/Testurio.UnitTests/Services/PlanEnforcementServiceTests.cs`:
  - `CheckMonthlyRunQuotaAsync_WhenTrialing_WithinWindow_CountsRunsInTrialPeriod`
    (repo called with `periodStart = TrialEndsAt-14d`, `periodEnd = TrialEndsAt`)
  - `CheckMonthlyRunQuotaAsync_WhenTrialing_WithinWindow_UnderLimit_DoesNotThrow`
  - `CheckMonthlyRunQuotaAsync_WhenTrialing_WithinWindow_AtLimit_Throws`
    (message contains trial end date)
  - `CheckMonthlyRunQuotaAsync_WhenTrialing_TrialExpired_Throws`
    (message says "trial has ended")
  - `CheckMonthlyRunQuotaAsync_WhenActive_UsesCalendarMonth` (regression — confirms non-trial path
    still uses calendar-month window and plan-tier message)
  - `CheckProjectLimitAsync_WhenTrialing_Below2Projects_Allows`
  - `CheckProjectLimitAsync_WhenTrialing_At2Projects_Throws`
    (message says "maximum of 2 projects")
  - `CheckProjectLimitAsync_WhenTrialing_Expired_Throws`
  - `CheckProjectLimitAsync_WhenActive_UsesPlanMaxProjects` (regression)

- [x] T005 [Test] Unit tests for `StatsRepository.GetQuotaUsageAsync` trial paths —
  `tests/Testurio.UnitTests/Services/StatsRepositoryQuotaTests.cs`:
  - `GetQuotaUsageAsync_WhenTrialing_WithinWindow_ReturnsTrialPeriodCounts`
    (`ResetsAt == TrialEndsAt`, `UsedThisMonth` reflects trial-window query params)
  - `GetQuotaUsageAsync_WhenTrialing_Expired_ReturnsZeroLimit`
  - `GetQuotaUsageAsync_WhenActive_ReturnsCalendarMonthCounts` (regression)

---

## Rationale

### Why modify PlanEnforcementService rather than a separate trial service

`IPlanEnforcementService` is the single authoritative source for all limit enforcement. Adding trial
awareness inside the existing methods keeps all enforcement in one place — no risk of partial
enforcement (e.g. webhook path enforces trial correctly but project creation endpoint doesn't).

### Why load UserSubscription directly in T001 / T002

Both modified methods need `subscription.TrialEndsAt` in addition to the `PlanDocument`. The current
`GetEffectivePlanAsync` loads the subscription internally but discards it after the null check. Rather
than restructuring the public interface, each method loads the subscription at its own entry point —
one clear, local read — then delegates plan loading to `_planRepository` directly.

### Why CountByUserForMonthAsync serves the trial window without changes

`ITestRunRepository.CountByUserForMonthAsync(userId, periodStart, periodEnd, ct)` already accepts
generic period bounds (renamed from `monthStart`/`nextMonthStart` during the merge with develop).
Passing `[TrialEndsAt.AddDays(-14), TrialEndsAt)` is a valid use of the same method — no new
repository method is required.

### Why TrialEndsAt.AddDays(-14) as trial start

`UserSubscription.TrialEndsAt` is the only trial-related date field on the entity. The 14-day trial
duration is a product constant, so `TrialEndsAt.AddDays(-14)` is the canonical trial start. If the
billing system ever stores an explicit `TrialStartedAt`, this derivation can be replaced — but no
migration is needed for v1.

### Cross-feature dependencies

| Feature | Dependency |
|---------|-----------|
| 0046 Plan Enforcement | `IPlanEnforcementService`, `PlanEnforcementService`, `PlanDocument`, `PlanLimits` — must be complete ✓ |
| 0001 Automatic Test Run Trigger | `JiraWebhookService`, `ADOWebhookService` catch `PlanLimitExceededException` → `QuotaExceeded` — must be complete ✓ |
| 0010 Dashboard | `StatsRepository.GetQuotaUsageAsync`, `QuotaUsage` model — must be complete ✓ |
| 0043 Real-Time Dashboard Updates | SSE quota events — explicitly out of scope for 0021 |

## Layer Tags

| Tag | Scope |
|-----|-------|
| `[Domain]` | Entities, interfaces, value objects — `Testurio.Core` |
| `[Infra]` | Repositories, service implementations — `Testurio.Infrastructure` |
| `[App]` | DTOs, services, validators |
| `[API]` | Minimal API endpoints, middleware — `Testurio.Api` |
| `[Test]` | Unit and integration test files |
