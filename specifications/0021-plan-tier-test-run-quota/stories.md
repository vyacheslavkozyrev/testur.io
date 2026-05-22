# User Stories — Plan-Tier Test Run Quota (0021)

## Context

Feature 0046 (Plan Enforcement) introduced `IPlanEnforcementService` which already handles:

- **No active plan** (`SubscriptionStatus.None`, `Expired`) → `CheckMonthlyRunQuotaAsync` throws
  `PlanLimitExceededException` (0-run limit path).
- **Active / CancelledPendingExpiry users** → monthly calendar-month quota from
  `PlanDocument.Limits.MaxTestRunsPerMonth`; project creation capped by `PlanDocument.Limits.MaxProjects`.
- **Webhook 200 OK contract** → `JiraWebhookService` and `ADOWebhookService` catch
  `PlanLimitExceededException`, return `WebhookProcessResult.QuotaExceeded`, and post a comment to
  the originating Jira issue / ADO work item.

Feature 0021 extends `PlanEnforcementService` and `StatsRepository` with **trial-period awareness**:
trialing users have their run quota evaluated against their 14-day trial window (not a calendar month),
and are capped at 2 projects regardless of plan tier.

## Out of Scope

- Real-time quota counter updates on the dashboard (feature 0043)
- Automatic `SubscriptionStatus.Trialing → Expired` transition when `TrialEndsAt` elapses
  (billing webhook pipeline — features 0015/0016)
- Per-project quota limits (quota is always per user account, shared across all projects)
- Soft-quota enforcement guarantees: two simultaneous webhook deliveries near the boundary may both
  pass; atomic enforcement is out of scope for v1
- `IQuotaPolicy` (replaced by `IPlanEnforcementService` from feature 0046 — not introduced here)
- Daily quota model (monthly period model used throughout)

---

## Stories

### US-001: Trial Users' Run Quota Applies to Their 14-Day Trial Window

**As a** user in the 14-day free trial
**I want** my test run quota measured against my trial period rather than a calendar month
**So that** quota resets on a date that is meaningful to me (when my trial ends), not an arbitrary month boundary

#### Acceptance Criteria

- [x] AC-001: When `subscription.Status == Trialing` and `subscription.TrialEndsAt > utcNow`,
  `CheckMonthlyRunQuotaAsync` counts `TestRun` documents created in the window
  `[TrialEndsAt.AddDays(-14), TrialEndsAt)` instead of the current calendar month.
- [x] AC-002: The limit applied within the trial window is `plan.Limits.MaxTestRunsPerMonth` from the
  user's associated `PlanDocument` (e.g. TestJunior = 50). The trial window shortens the counting
  period, not the per-period run limit.
- [x] AC-003: If `TrialEndsAt <= utcNow` but `Status` is still `Trialing` (billing webhook has not yet
  updated the status), the user is treated as having 0 runs allowed; `PlanLimitExceededException`
  is thrown with message `"Your trial has ended. Purchase a plan to run more tests."`,
  `limitName = "maxTestRunsPerMonth"`, `requiredPlan = "Test Junior"`.
- [x] AC-004: The exception message for an in-trial quota-exceeded case includes the trial end date:
  `"Your trial allows {maxRuns} test runs. Trial ends on {TrialEndsAt:yyyy-MM-dd}."`.
- [x] AC-005: For `Active`, `CancelledPendingExpiry`, and `PaymentFailed` users,
  `CheckMonthlyRunQuotaAsync` behaviour is unchanged — calendar month window, plan-tier limit,
  reset message includes the first day of the next month.

---

### US-002: Trial Users Can Create At Most 2 Projects

**As a** user in the 14-day free trial
**I want** to be able to create up to 2 projects
**So that** I can evaluate Testurio with my real products before purchasing a plan

#### Acceptance Criteria

- [x] AC-006: When `subscription.Status == Trialing` and `TrialEndsAt > utcNow`,
  `CheckProjectLimitAsync` enforces a cap of 2 projects regardless of the plan tier's
  `MaxProjects` value.
- [x] AC-007: Attempting to create a 3rd project during an active trial throws
  `PlanLimitExceededException` with `limitName = "maxProjects"`, `requiredPlan = "Test Junior"`,
  and message `"Your trial allows a maximum of 2 projects. Purchase a plan to create more."`.
- [x] AC-008: When `TrialEndsAt <= utcNow` with `Status` still `Trialing` (expired trial, billing
  webhook pending), project creation is blocked entirely (0-limit path — same message as the
  no-plan path with `"Your trial has ended."`).
- [x] AC-009: For `Active`, `CancelledPendingExpiry`, and `PaymentFailed` users,
  `CheckProjectLimitAsync` behaviour is unchanged — uses `plan.Limits.MaxProjects`.

---

### US-003: Dashboard Reflects Trial-Period Usage for Trialing Users

**As a** trialing user
**I want** the dashboard quota bar to show how many runs I have used during my trial period
**So that** I can track my remaining trial runs accurately and know when the trial ends

#### Acceptance Criteria

- [x] AC-010: `GET /v1/stats/dashboard` for a trialing user within the trial window returns
  `QuotaUsage` where:
  - `UsedThisMonth` = count of `TestRun` documents created in
    `[TrialEndsAt.AddDays(-14), TrialEndsAt)`
  - `MonthlyLimit` = `plan.Limits.MaxTestRunsPerMonth` (the plan-tier limit)
  - `ResetsAt` = `TrialEndsAt` (not the first day of next month)
- [x] AC-011: For an expired-trial user (`TrialEndsAt <= utcNow` and `Status == Trialing`),
  `MonthlyLimit = 0` and `ResetsAt` is set to `TrialEndsAt` (UI renders "No active plan").
- [x] AC-012: For `Active` and `CancelledPendingExpiry` users, dashboard quota behaviour is
  unchanged — calendar month window, plan-tier limit, next-month `ResetsAt`.
