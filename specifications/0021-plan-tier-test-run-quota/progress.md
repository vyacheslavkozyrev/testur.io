# Progress — Plan-Tier Test Run Quota (0021)

## Phase Status

| Phase     | Status      | Date       | Notes |
| --------- | ----------- | ---------- | ----- |
| Specify   | ✅ Complete | 2026-05-19 |       |
| Plan      | ✅ Complete | 2026-05-19 |       |
| Implement | ✅ Complete | 2026-05-19 |       |
| Review    | ✅ Complete | 2026-05-20 |       |
| Test      | ⏳ Pending  |            |       |

---

## Implementation Notes

_Populated by `/implement [####]`_

---

## Review

**Date:** 2026-05-20

### Findings and Fixes Applied

| Severity | Finding | Fix Applied |
|----------|---------|-------------|
| Warning | `PostQuotaCommentAsync` used `DateTimeOffset.UtcNow.Date.AddDays(1)` which returns a `DateTime` with `Kind=Unspecified` instead of a `DateTimeOffset`. Inconsistent with the rest of the codebase. | Replaced with `new DateTimeOffset(utcNow.Year, utcNow.Month, utcNow.Day, 0, 0, 0, TimeSpan.Zero).AddDays(1)` in `JiraWebhookService`. |
| Warning | `QuotaPolicy` used a wildcard `_` arm that silently returned `0` for any unrecognised `SubscriptionPlan` value. Future enum additions would silently block all test runs for those plan tiers. | Replaced wildcard arm with a `[LoggerMessage]` warning and explicit `0` return. Added `ILogger<QuotaPolicy>` constructor injection. Updated DI registration and `QuotaPolicyTests` to use `NullLogger`. |
| Suggestion | ADO quota-check logic was inlined directly in `ProcessAsync` rather than extracted to a helper, unlike Jira which already used `CheckQuotaAsync`. Code duplication risk for future maintenance. | Extracted to a dedicated `CheckQuotaAsync(Project, string workItemId, CancellationToken)` method in `ADOWebhookService`. |

### Remaining Issues

- **T016 ADO integration test (AC-022):** The plan requires "ADO webhook returns `200 OK` on quota exhaustion" as an HTTP-level integration test. This cannot be implemented because there is no ADO work-item webhook HTTP endpoint registered in `Program.cs` (only `ADOWebhookService` exists as a service; no route handler comparable to `JiraWebhookController` exists for ADO). The behaviour is validated at the unit-test level in `ADOWebhookServiceQuotaTests.cs`. This test must be added when a future feature maps the ADO webhook HTTP route.
- **Pre-existing build failure in `BillingServiceTests.cs`** (line 78 — `CS1503`): unrelated to feature 0021, existed before this branch.

---

## Test Results

_Populated by `/test [####]`_

---

## Amendments

_Populated when spec or plan changes after initial approval. Format:_

```
### Amendment — YYYY-MM-DD
**Changed**: [which documents were updated]
**Reason**: [why the change was needed]
**Impact**: [phases that need to re-run as a result]
```
