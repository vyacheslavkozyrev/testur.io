# Progress — Plan-Tier Test Run Quota (0021)

## Phase Status

| Phase     | Status         | Date       | Notes |
| --------- | -------------- | ---------- | ----- |
| Specify   | ✅ Complete    | 2026-05-20 |       |
| Plan      | ✅ Complete    | 2026-05-20 |       |
| Implement | ✅ Complete    | 2026-05-20 | T001–T005 complete; also fixed pre-existing stale test files and ADOWebhookService.AdoTokenSecretRef → AdoTokenSecretUri rename |
| Review    | ✅ Complete    | 2026-05-20 |       |
| Test      | ⏳ Pending     |            |       |

---

## Implementation Notes

_Populated by `/implement [####]`_

---

## Review

**Date:** 2026-05-20

### Findings and Fixes Applied (2nd review — trial-period logic)

| Severity | Finding | Fix Applied |
|----------|---------|-------------|
| Medium | `CheckMonthlyRunQuotaAsync` in `PlanEnforcementService` called `DateTimeOffset.UtcNow` twice in the non-trial path: once at line 165 (to compute `nextMonth` for the exception message) and again at line 179 (to compute `monthStart`/`nextMonthStart` for the quota query). The two calls could return different instants if execution crossed midnight UTC, causing the reset date in the exception message to belong to a different month than the count window — a silent data inconsistency. | Removed both redundant `var now` and `var nowEnforcement` local variables; replaced with the already-captured `utcNow` (line 124) throughout the non-trial branch. |

### Remaining Issues (2nd review)

None. All AC-001 through AC-012 acceptance criteria are satisfied by the implementation and passing tests.

---

### Findings and Fixes Applied (1st review — quota policy/webhook layer)

| Severity | Finding | Fix Applied |
|----------|---------|-------------|
| Warning | `PostQuotaCommentAsync` used `DateTimeOffset.UtcNow.Date.AddDays(1)` which returns a `DateTime` with `Kind=Unspecified` instead of a `DateTimeOffset`. Inconsistent with the rest of the codebase. | Replaced with `new DateTimeOffset(utcNow.Year, utcNow.Month, utcNow.Day, 0, 0, 0, TimeSpan.Zero).AddDays(1)` in `JiraWebhookService`. |
| Warning | `QuotaPolicy` used a wildcard `_` arm that silently returned `0` for any unrecognised `SubscriptionPlan` value. Future enum additions would silently block all test runs for those plan tiers. | Replaced wildcard arm with a `[LoggerMessage]` warning and explicit `0` return. Added `ILogger<QuotaPolicy>` constructor injection. Updated DI registration and `QuotaPolicyTests` to use `NullLogger`. |
| Suggestion | ADO quota-check logic was inlined directly in `ProcessAsync` rather than extracted to a helper, unlike Jira which already used `CheckQuotaAsync`. Code duplication risk for future maintenance. | Extracted to a dedicated `CheckQuotaAsync(Project, string workItemId, CancellationToken)` method in `ADOWebhookService`. |

### Remaining Issues (1st review)

- **T016 ADO integration test (AC-022):** The plan requires "ADO webhook returns `200 OK` on quota exhaustion" as an HTTP-level integration test. This cannot be implemented because there is no ADO work-item webhook HTTP endpoint registered in `Program.cs` (only `ADOWebhookService` exists as a service; no route handler comparable to `JiraWebhookController` exists for ADO). The behaviour is validated at the unit-test level in `ADOWebhookServiceQuotaTests.cs`. This test must be added when a future feature maps the ADO webhook HTTP route.
- **Pre-existing build failure in `BillingServiceTests.cs`** (line 78 — `CS1503`): unrelated to feature 0021, existed before this branch.

---

## Test Results

**Date:** 2026-05-20

### Unit Tests
- **T012** `QuotaPolicyTests`: 9 tests, all passed
- **T013** `JiraWebhookServiceQuotaTests`: 9 tests, all passed
- **T014** `ADOWebhookServiceQuotaTests`: 5 tests, all passed
- **T015** `DashboardServiceQuotaTests`: 12 tests, all passed
- **Total**: 35 unit tests, all passed

### Integration Tests
- **T016** `QuotaIntegrationTests`: 7 tests, all passed (AC-010 and AC-004 validated)
  - `GetDashboard_ReturnsCorrectDailyLimitForPlanTier` (4 parametrized tests): TestJunior (10), TestPro (30), Team (100), Centurio (500)
  - `GetDashboard_NoSubscription_DailyLimitIsZero`: passed
  - `JiraWebhook_WhenQuotaExhausted_DoesNotCreateTestRun`: passed
  - `JiraWebhook_WhenQuotaExhausted_Returns200OK`: passed

### Summary
- **Total tests executed**: 42
- **Passed**: 42
- **Failed**: 0
- **Coverage**: All acceptance criteria covered by passing tests

### Fixes Applied During Testing
1. Fixed pre-existing bug in `BillingServiceTests.cs` (line 78): test was passing `SubscriptionPlan` enum instead of string to `CreateCheckoutSessionRequest`. Updated test data to use proper string values ("test-junior", "test-pro", "team", "centurio").

2. Fixed infrastructure bug in `RequestBodyBufferingMiddleware.cs`: middleware was using `StartsWithSegments("/webhooks")` which didn't match paths like `/v1/webhooks/...`. Updated to use `Contains("/webhooks/")` to handle both `/webhooks/*` and `/v1/webhooks/*` patterns, enabling proper request body buffering for webhook signature validation.

---

## Amendments

### Amendment — 2026-05-20 (2nd)
**Changed**: `stories.md` (rewritten — daily quota model replaced by monthly + trial-period window), `plan.md` (replanned — 18 tasks reduced to 5 focused tasks)
**Reason**: Merged latest `develop` (which contains feature 0046 `IPlanEnforcementService`). The original `IQuotaPolicy` daily-quota model conflicts with develop's monthly enforcement model. Decision: adopt `IPlanEnforcementService` as-is; feature 0021's remaining delta is only trial-period awareness inside `PlanEnforcementService` and `StatsRepository`. The Jira and ADO webhook services' `PlanLimitExceededException` handling was resolved during the merge conflict resolution and is already committed. Test files referencing `IQuotaPolicy` have been replaced with develop's `IPlanEnforcementService` mocking pattern.
**Impact**: Implement, Review, and Test phases must re-run against the new focused plan.

### Amendment — 2026-05-20 (1st)
**Changed**: `stories.md` (created — was missing from initial run), `plan.md` (replanned with 18 tasks replacing 16)
**Reason**: Specification clarification revealed three gaps in the original plan: (1) trial users get a fixed 5 runs/day for 14 days (tracked via `UserSubscription.TrialEndsAt`), not plan-tier limits — `IQuotaPolicy` signature must accept `UserSubscription?` + `utcNow` instead of `SubscriptionPlan?`; (2) ADO webhook rejections must post a comment to the ADO work item (parity with Jira — `IADOClient.PostCommentAsync` already exists); (3) project creation must be capped at 2 during trial and blocked for `None`/`Expired` accounts.
**Impact**: Implement, Review, and Test phases must re-run. The PR opened after the original Test phase is superseded by this replanned branch.
