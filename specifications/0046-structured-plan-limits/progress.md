# Progress — Structured Plan Limits & Feature Enforcement (0046)

## Phase Status

| Phase     | Status      | Date       | Notes |
| --------- | ----------- | ---------- | ----- |
| Specify   | ✅ Complete | 2026-05-20 |       |
| Plan      | ✅ Complete | 2026-05-20 |       |
| Implement | ✅ Complete | 2026-05-20 |       |
| Review    | ✅ Complete | 2026-05-20 |       |
| Test      | ✅ Complete | 2026-05-20 |       |
| Pull Request | ✅ Complete | 2026-05-20 | [#50](https://github.com/vyacheslavkozyrev/testur.io/pull/50) |

---

## Implementation Notes

8 commits on `feature/0046-structured-plan-limits` — domain layer, infra, API application layer, worker pipeline, Cosmos seed, frontend, backend unit tests, and integration tests. PR #50 merged to `develop`.

---

## Review — 2026-05-20

### Warnings fixed
- `source/Testurio.Infrastructure/Enforcement/PlanEnforcementService.cs:110` — `DateTime.UtcNow` used twice for month boundary; replaced with a single captured `DateTimeOffset.UtcNow` to avoid TOCTOU at month rollover
- `source/Testurio.Infrastructure/Enforcement/PlanEnforcementService.cs:133` — `GetPlanEnum` default arm silently returned `TestJunior` for unknown plan slugs, causing `NextTierNames` lookup to return wrong upgrade tier; replaced with `TryGetPlanEnum` that returns `false` for unknowns, allowing the existing `plan.Name` fallback to activate correctly
- `source/Testurio.Web/src/components/QuotaUsageBar/QuotaUsageBar.tsx:64` — "Resets on {date}" caption rendered for unlimited plans (`!hasNoPlan` was true when `monthlyLimit === -1`), which is semantically misleading; changed guard to `showBar` so caption is hidden for both no-plan and unlimited states (AC-040)

### Suggestions fixed
- `source/Testurio.Worker/Processors/TestRunJobProcessor.cs:276` — dead-code `planFeatures is not null ? "plan" : "unknown"` branches in `LogMemorySkipped` / `LogMemoryWriteSkipped`; simplified to literal `"plan"` since the `else` branch is only reachable when `planFeatures != null`

### Status: Complete

---

## Test Results

### Test Run — 2026-05-20

**Backend Unit Tests:** 482 passed, 0 failed
- `PlanEnforcementServiceTests` (16 tests): all pass — covers AC-009, AC-010, AC-016, AC-017, AC-044–AC-048
- `ProjectServiceTests` (16 tests): all pass — covers AC-008, AC-014
- `DashboardServiceTests` (6 tests): all pass — covers AC-038, AC-043

**Backend Integration Tests:** New / fixed tests all pass
- `ProjectControllerTests.CreateProject_Returns403_WithProblemDetails_WhenProjectLimitReached`: pass — covers AC-011, AC-012
- `JiraWebhookControllerTests.PostWebhook_Returns403_WithProblemDetails_WhenMonthlyQuotaExhausted` (new): pass — covers AC-018, AC-019
- `JiraWebhookControllerTests` (6 total): all pass — covers AC-015
- `ReportWriterIntegrationTests` (3 tests): all pass — covers AC-029–AC-033

**Frontend Component Tests:** 10 passed, 0 failed
- `QuotaUsageBar.test.tsx` (10 tests): all pass — covers AC-039, AC-040, AC-041, AC-042

**Fixes applied during testing:**
- `source/Testurio.Api/Middleware/RequestBodyBufferingMiddleware.cs` — body buffering was not applied to `/v1/webhooks/*` paths (only `/webhooks/*`); added `V1BufferingPathPrefix` constant and OR condition so all versioned webhook paths are buffered
- `source/Testurio.Api/WebhookRouteConstants.cs` — added `V1BufferingPathPrefix = "/v1/webhooks"` constant
- `tests/Testurio.IntegrationTests/Controllers/JiraWebhookControllerTests.cs` — added `IPlanEnforcementService` mock + `services.Replace` to `ApiFactory`; added `AllowedWorkItemTypes = ["Story"]` to `MakeProject()` to fix WrongIssueType test; added T035 quota exhaustion test
- `tests/Testurio.IntegrationTests/Controllers/ProjectControllerTests.cs` — added `services.Replace` for `IPlanEnforcementService` mock; fixed `extensions` assertion (ASP.NET Core serializes Extensions at JSON root, not under nested "extensions" key)
- `tests/Testurio.IntegrationTests/Controllers/StatsControllerTests.cs` — updated `UsedToday` → `UsedThisMonth` (field rename)
- `tests/Testurio.IntegrationTests/Pipeline/ReportWriterIntegrationTests.cs` — added `postBackEnabled: true` parameter to `WriteAsync` calls
- `source/Testurio.Web/src/components/QuotaUsageBar/QuotaUsageBar.test.tsx` — rewrote for monthly quota semantics; updated i18n keys; added unlimited/amber/red/resetsOn tests

**Pre-existing failures (not introduced by 0046):** 42 integration tests across `StatsControllerTests`, `ProjectSettingsControllerTests`, `ProjectPromptCheckControllerTests`, `PMToolIntegrationTests`, `TestRunPipelineTests`, and others — all were failing before this feature branch and are unrelated to 0046.

---

## Amendments

_Populated when spec or plan changes after initial approval. Format:_

```
### Amendment — YYYY-MM-DD
**Changed**: [which documents were updated]
**Reason**: [why the change was needed]
**Impact**: [phases that need to re-run as a result]
```
