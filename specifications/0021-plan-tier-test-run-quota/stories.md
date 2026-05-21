# User Stories — Plan-Tier Test Run Quota (0021)

## Out of Scope

The following are explicitly **not** part of this feature:

- Real-time quota counter updates on the dashboard (deferred to feature 0043)
- Automatic transition of `SubscriptionStatus` from `Trialing` to `Expired` when `TrialEndsAt` elapses (handled by the billing webhook pipeline — feature 0015/0016)
- Per-project quota limits (quota is always per user account, shared across all projects)
- Soft-quota enforcement guarantees: two simultaneous webhook deliveries near the boundary may both pass; atomic enforcement is out of scope for v1

---

## Stories

### US-001: Jira Trigger Rejected When Daily Quota Exhausted

**As a** QA lead on a paid plan
**I want to** receive a Jira comment when my daily test run quota is exhausted
**So that** I know why no test ran and when the counter resets

#### Acceptance Criteria

- [ ] AC-001: When `usedToday >= dailyLimit` for the user's account, the Jira webhook handler returns `200 OK` and does not enqueue a test run.
- [ ] AC-002: The system posts a comment to the originating Jira issue containing the quota-exhausted message and the next midnight UTC reset time (formatted as ISO 8601 date, e.g. "resets at 2026-05-21T00:00:00Z").
- [ ] AC-003: `JiraWebhookService.ProcessAsync` returns `WebhookProcessResult.QuotaExceeded`.
- [ ] AC-004: The quota check runs after work-item-type filtering but before story parsing or test enqueueing.
- [ ] AC-005: `usedToday` counts all `TestRun` documents created within the current UTC calendar day (00:00:00Z inclusive to 00:00:00Z next day exclusive), regardless of run status.

---

### US-002: ADO Trigger Rejected When Daily Quota Exhausted

**As a** QA lead using Azure DevOps
**I want to** receive an ADO work item comment when my daily quota is exhausted
**So that** I have the same visibility as Jira users and don't wonder why runs stopped

#### Acceptance Criteria

- [ ] AC-006: When `usedToday >= dailyLimit`, the ADO webhook handler returns `200 OK` and does not enqueue a test run.
- [ ] AC-007: The system posts a comment to the originating ADO work item with the quota-exhausted message and next midnight UTC reset time (same format as AC-002).
- [ ] AC-008: `ADOWebhookService.ProcessAsync` returns `WebhookProcessResult.QuotaExceeded`.
- [ ] AC-009: The quota check for ADO runs after work-item-type filtering, before story parsing or test enqueueing.

---

### US-003: Free and Expired Accounts Cannot Trigger Runs

**As a** user with no active subscription (status `None` or `Expired`)
**I want to** be clearly notified when I attempt to trigger a test run
**So that** I understand I need to purchase a plan before runs can execute

#### Acceptance Criteria

- [ ] AC-010: When a Jira webhook arrives for a user with `SubscriptionStatus.None` or `SubscriptionStatus.Expired`, the system returns `200 OK`, does not enqueue, and posts a "no active subscription — purchase a plan to run tests" comment to the Jira issue.
- [ ] AC-011: When an ADO webhook arrives for a `None`/`Expired` user, the system returns `200 OK`, does not enqueue, and posts the same "no active subscription" comment to the ADO work item.
- [ ] AC-012: The "no active subscription" message is distinct from the quota-exhausted message — it does not include a reset time.
- [ ] AC-013: `ProcessAsync` returns `WebhookProcessResult.QuotaExceeded` for both Jira and ADO no-subscription rejections (reuses the same enum value).

---

### US-004: Trial Users Get 5 Runs Per Day for 14 Days

**As a** user in the 14-day free trial
**I want to** trigger up to 5 test runs per day
**So that** I can evaluate the product's core capability before committing to a plan

#### Acceptance Criteria

- [ ] AC-014: A user with `SubscriptionStatus.Trialing` and `TrialEndsAt > utcNow` has a daily limit of 5 runs, regardless of which `SubscriptionPlan` value is set on their subscription.
- [ ] AC-015: Once `TrialEndsAt <= utcNow`, the trial is considered expired and `dailyLimit` falls to 0 (treated identically to `Expired`), even if `SubscriptionStatus` is still `Trialing` (status update is the billing pipeline's responsibility).
- [ ] AC-016: When a trialing user exhausts their 5-run daily quota, the system posts the same quota-exhausted comment with reset time as a paid user (AC-002 / AC-007).
- [ ] AC-017: Users with `SubscriptionStatus.CancelledPendingExpiry` or `PaymentFailed` retain their plan-tier daily limit (10/30/100/500) as they are still within a paid period.

---

### US-005: Project Creation Limited During Trial

**As a** user in the 14-day free trial
**I want to** create up to 2 projects
**So that** I can evaluate Testurio with my real products before purchasing

#### Acceptance Criteria

- [ ] AC-018: A trialing user (within `TrialEndsAt`) can create at most 2 projects. Attempting to create a third returns HTTP `409 Conflict` with a `ProblemDetails` response explaining the limit and directing the user to purchase a plan.
- [ ] AC-019: After trial expiry (`TrialEndsAt <= utcNow` or `Status == Expired`), no new projects can be created. The same `409 Conflict` response is returned.
- [ ] AC-020: Users with `SubscriptionStatus.None` (never started a trial) cannot create any projects. `POST /v1/projects` returns `409 Conflict` with a "no active subscription" explanation.
- [ ] AC-021: Users with `Active`, `CancelledPendingExpiry`, or `PaymentFailed` status have no project count limit.
- [ ] AC-022: The project creation limit applies only to project creation — existing projects remain accessible for viewing and running (within the daily run quota).

---

### US-006: Dashboard Shows Correct Quota Usage

**As a** QA lead
**I want** the dashboard quota bar to reflect my actual plan-tier or trial daily limit
**So that** I can gauge how many test runs I have left today

#### Acceptance Criteria

- [ ] AC-023: `GET /v1/stats/dashboard` returns `QuotaUsage.dailyLimit` equal to the plan-tier limit for `Active` users: TestJunior=10, TestPro=30, Team=100, Centurio=500.
- [ ] AC-024: For trialing users within `TrialEndsAt`, `dailyLimit` is 5.
- [ ] AC-025: For `None`, `Expired`, or post-`TrialEndsAt` users, `dailyLimit` is 0.
- [ ] AC-026: `QuotaUsage.usedToday` reflects the count of all `TestRun` documents created today (UTC) for the user, regardless of status.

---

### US-007: IQuotaPolicy as Single Authoritative Source

**As a** developer
**I want** a single interface that resolves both daily run limits and project creation limits
**So that** quota rules can be tested in isolation and applied consistently across webhook services, project creation, and the dashboard

#### Acceptance Criteria

- [ ] AC-027: `IQuotaPolicy.GetDailyLimit(UserSubscription? subscription, DateTimeOffset utcNow)` is the authoritative source for daily run limits. No other code path hard-codes plan-to-limit mappings.
- [ ] AC-028: `IQuotaPolicy.GetMaxProjects(UserSubscription? subscription, DateTimeOffset utcNow)` returns `2` for active trial users, `0` for `None`/`Expired`/expired-trial users, and `null` (unlimited) for `Active`/`CancelledPendingExpiry`/`PaymentFailed` users.
