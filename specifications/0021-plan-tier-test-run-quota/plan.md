# Implementation Plan — Plan-Tier Test Run Quota (0021)

## Tasks

### Domain — Quota Policy Interface

- [ ] T001 [Domain] Update `IQuotaPolicy`: change `GetDailyLimit(SubscriptionPlan? plan)` to `GetDailyLimit(UserSubscription? subscription, DateTimeOffset utcNow): int`; add `GetMaxProjects(UserSubscription? subscription, DateTimeOffset utcNow): int?` (`null` = unlimited) — `source/Testurio.Core/Interfaces/IQuotaPolicy.cs`

### Infrastructure — Quota Policy Implementation

- [ ] T002 [Infra] Update `QuotaPolicy` to implement the revised `IQuotaPolicy` signatures:
  - `GetDailyLimit`: `Trialing` with `TrialEndsAt > utcNow` → 5; `Trialing` with expired trial, `None`, or `Expired` → 0; `Active`, `CancelledPendingExpiry`, `PaymentFailed` → plan-tier limit (TestJunior=10, TestPro=30, Team=100, Centurio=500); `null` subscription → 0; unrecognised `SubscriptionPlan` → log warning, return 0.
  - `GetMaxProjects`: active trial → 2; `None`, `Expired`, expired-trial → 0; `Active`, `CancelledPendingExpiry`, `PaymentFailed` → `null`.
  - `source/Testurio.Infrastructure/Quota/QuotaPolicy.cs`
- [ ] T003 [Infra] Update DI registration of `QuotaPolicy` if constructor signature changed — `source/Testurio.Infrastructure/DependencyInjection.cs`

### Application — Webhook Services Enforcement

- [ ] T004 [App] Confirm `QuotaExceeded` value exists in `WebhookProcessResult` enum; add if missing — `source/Testurio.Api/Services/WebhookProcessResult.cs`
- [ ] T005 [App] Update `JiraWebhookService.CheckQuotaAsync`: pass `UserSubscription?` + `utcNow` to `IQuotaPolicy.GetDailyLimit` instead of `subscription.Plan`; handle `Trialing`-with-expired-trial as zero-quota path — `source/Testurio.Api/Services/JiraWebhookService.cs`
- [ ] T006 [App] Update `ADOWebhookService.CheckQuotaAsync`: pass `UserSubscription?` + `utcNow` to `IQuotaPolicy.GetDailyLimit`; call `IADOClient.PostCommentAsync` on both quota-exhausted and no-subscription rejection paths (mirroring Jira behaviour) — `source/Testurio.Api/Services/ADOWebhookService.cs`

### Domain — Test Run Count Query

- [ ] T007 [Domain] Confirm `CountTodayAsync(string userId, DateTimeOffset windowStart, DateTimeOffset windowEnd): Task<int>` exists on `ITestRunRepository`; add if missing — `source/Testurio.Core/Repositories/ITestRunRepository.cs`

### Infrastructure — Test Run Count Implementation

- [ ] T008 [Infra] Confirm `CountTodayAsync` implementation exists in `TestRunRepository`; add if missing — `source/Testurio.Infrastructure/Cosmos/TestRunRepository.cs`

### Application — Project Creation Limit

- [ ] T009 [App] Enforce project creation limit in the project creation endpoint or service: inject `IQuotaPolicy` and `IUserSubscriptionRepository`; before creating the project resolve the subscription, call `IQuotaPolicy.GetMaxProjects`, count existing projects for the user, and return `409 Conflict` with `ProblemDetails` if the limit is reached — `source/Testurio.Api/Services/ProjectService.cs` (or equivalent project creation path)

### Application — Dashboard Quota Fix

- [ ] T010 [App] Update `DashboardService`: pass `UserSubscription?` + `utcNow` to `IQuotaPolicy.GetDailyLimit` instead of `subscription?.Plan` — `source/Testurio.Api/Services/DashboardService.cs`

### Infrastructure — Stats Repository Quota Signature

- [ ] T011 [Infra] Confirm `StatsRepository.GetQuotaUsageAsync` accepts `int dailyLimit` parameter; add if missing — `source/Testurio.Infrastructure/Cosmos/StatsRepository.cs`
- [ ] T012 [Domain] Confirm `IStatsRepository.GetQuotaUsageAsync` signature accepts `int dailyLimit`; update if missing — `source/Testurio.Core/Interfaces/IStatsRepository.cs`

### Tests

- [ ] T013 [Test] Update `QuotaPolicyTests`: add trial-within-14-days → 5, trial-past-`TrialEndsAt` → 0, `None` → 0, `Expired` → 0, `Active` plan tiers → correct limits, `CancelledPendingExpiry` → plan-tier limit, `PaymentFailed` → plan-tier limit, null subscription → 0; add `GetMaxProjects` tests for all status × trial-window combinations — `tests/Testurio.UnitTests/Services/QuotaPolicyTests.cs`
- [ ] T014 [Test] Update `JiraWebhookServiceQuotaTests`: update test setup to pass `UserSubscription` objects (with `TrialEndsAt`) rather than bare `SubscriptionPlan?`; add trial-user quota-exhausted path and expired-trial zero-quota path — `tests/Testurio.UnitTests/Services/JiraWebhookServiceQuotaTests.cs`
- [ ] T015 [Test] Update `ADOWebhookServiceQuotaTests`: add assertions that `IADOClient.PostCommentAsync` is called with the quota-exhausted message on rejection; add no-subscription path test that also asserts the comment is posted — `tests/Testurio.UnitTests/Services/ADOWebhookServiceQuotaTests.cs`
- [ ] T016 [Test] Unit tests for project creation limit: trial user at limit (2 projects) returns `409`; trial user below limit succeeds; `None`/`Expired` user returns `409`; `Active` user with 10 projects succeeds — `tests/Testurio.UnitTests/Services/ProjectServiceQuotaTests.cs`
- [ ] T017 [Test] Update `DashboardServiceQuotaTests`: add trial-user path asserting `dailyLimit == 5`; add expired-trial path asserting `dailyLimit == 0` — `tests/Testurio.UnitTests/Services/DashboardServiceQuotaTests.cs`
- [ ] T018 [Test] Update `QuotaIntegrationTests`: add trial-user `dailyLimit == 5` assertion; add ADO webhook quota-exhausted `200 OK` assertion (if ADO HTTP route is mapped by this point) — `tests/Testurio.IntegrationTests/Controllers/QuotaIntegrationTests.cs`

---

## Rationale

### Task ordering

**`IQuotaPolicy` updated first (T001).** The interface lives in `Testurio.Core` and all downstream layers — `QuotaPolicy`, `JiraWebhookService`, `ADOWebhookService`, `DashboardService`, and the new project creation check — depend on its signature. The new signature accepts `UserSubscription?` + `DateTimeOffset utcNow` instead of `SubscriptionPlan?`, enabling trial-period logic without adding a second method.

**`QuotaPolicy` implementation before callers (T002–T003 before T005–T006, T009–T010).** The concrete implementation must compile against the updated interface before any service that injects `IQuotaPolicy` can be modified.

**`WebhookProcessResult` confirmed before webhook services (T004).** Both Jira and ADO services return `QuotaExceeded`; the value must exist first.

**`CountTodayAsync` confirmed before webhook service changes (T007–T008 before T005–T006).** The quota check in both webhook services reads today's run count. Confirmed-or-added before the services are updated.

**ADO comment posting is already in `IADOClient.PostCommentAsync`** — no new interface task is required. T006 updates `ADOWebhookService` to call it; this is the only change needed to add ADO comment posting parity with Jira.

**Project creation limit after `IQuotaPolicy` is stable (T009 after T001–T003).** The new `GetMaxProjects` method must be defined before the project service can call it.

**Dashboard fix after quota policy (T010–T012).** `DashboardService` (T010) needs the updated `GetDailyLimit` signature; `IStatsRepository` / `StatsRepository` changes (T011–T012) are confirmed-or-added alongside it.

**Tests last (T013–T018).** All test tasks follow the implementation tasks they exercise. Unit tests mock dependencies; integration tests require the full DI container.

### Cross-feature dependencies

- **Feature 0015 (Plan Purchase):** `SubscriptionPlan`, `SubscriptionStatus`, `UserSubscription` (including `TrialEndsAt`) are introduced by feature 0015. Feature 0021 reads `TrialEndsAt` to determine the 14-day trial window. Feature 0015 must be complete.
- **Feature 0001 (Automatic Test Run Trigger):** `JiraWebhookService`, `ADOWebhookService`, `ITestRunRepository`, and `WebhookProcessResult` are introduced by feature 0001. Feature 0021 extends these. Feature 0001 must be complete.
- **Feature 0010 (Dashboard):** `QuotaUsage` model, `IStatsRepository.GetQuotaUsageAsync`, and `StatsRepository` are introduced by feature 0010. Feature 0021 corrects the hard-coded `dailyLimit = 0` placeholder. Feature 0010 must be complete.
- **Feature 0006 (Project Creation):** `ProjectService` and `POST /v1/projects` are introduced by feature 0006. Feature 0021 adds a quota check to project creation. Feature 0006 must be complete.
- **Feature 0043 (Real-Time Dashboard Updates):** The SSE stream may carry quota increment events in the future. This is explicitly out of scope for feature 0021.

### Architectural decisions

**`IQuotaPolicy` accepts `UserSubscription?` + `utcNow` rather than individual fields.** Passing the full entity keeps the call site simple and avoids proliferating `SubscriptionPlan?`, `SubscriptionStatus`, `DateTimeOffset?` parameters. `utcNow` is injected as a parameter (not read internally via `DateTimeOffset.UtcNow`) to keep `QuotaPolicy` a pure, deterministic function — trivial to unit-test with frozen time.

**Trial window computed from `UserSubscription.TrialEndsAt`.** The field already exists on `UserSubscription` (feature 0015). No new domain fields are required.

**`GetMaxProjects` returns `int?` (null = unlimited).** `null` is the clearest signal that no upper bound exists for paid users, avoiding magic numbers like `int.MaxValue`.

**ADO comment posting reuses `IADOClient.PostCommentAsync`** which already exists from a prior feature. No new interface changes are needed; feature 0021 simply enables the call path that `ADOWebhookService` previously bypassed.

**No persisted counter.** The daily quota is always computed on the fly by counting `TestRun` documents within the UTC calendar-day window. Eliminates counter drift and reset-job failures at the cost of a cross-partition fan-out query — acceptable on the webhook path, which fires at most once per delivery.

**Soft quota enforcement.** Two simultaneous webhook deliveries near the boundary may both pass. Accepted per US-007. Atomic enforcement would require distributed locking — unnecessary for v1.

## Layer Tags

| Tag | Scope |
|-----|-------|
| `[Migration]` | EF Core migration files |
| `[Domain]` | Entities, interfaces, value objects — `Testurio.Core` |
| `[Infra]` | Repositories, EF config, DI registration — `Testurio.Infrastructure` |
| `[App]` | DTOs, services, validators |
| `[API]` | Minimal API endpoints, route groups, middleware — `Testurio.Api` |
| `[Config]` | App configuration, constants, feature flags |
| `[UI]` | Types, API clients, hooks, MSW handlers, components, pages, i18n translation keys, route registration |
| `[Test]` | Unit, integration, and frontend component test files |
