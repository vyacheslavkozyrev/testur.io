# Implementation Plan — Plan-Tier Test Run Quota (0021)

## Tasks

### Domain — Quota Policy Interface

- [x] T001 [Domain] Add `IQuotaPolicy` interface with `GetDailyLimit(SubscriptionPlan? plan): int` — `source/Testurio.Core/Interfaces/IQuotaPolicy.cs`

### Infrastructure — Quota Policy Implementation

- [x] T002 [Infra] Implement `QuotaPolicy` class: maps `SubscriptionPlan` to daily limits (TestJunior=10, TestPro=30, Team=100, Centurio=500); returns `0` for `null` — `source/Testurio.Infrastructure/Quota/QuotaPolicy.cs`
- [x] T003 [Infra] Register `QuotaPolicy` as singleton `IQuotaPolicy` in DI — `source/Testurio.Infrastructure/DependencyInjection.cs`

### Application — Webhook Services Enforcement

- [x] T004 [App] Add `QuotaExceeded` value to `WebhookProcessResult` enum — `source/Testurio.Api/Services/WebhookProcessResult.cs`
- [x] T005 [App] Inject `IQuotaPolicy` and `IUserSubscriptionRepository` into `JiraWebhookService`; add quota check in `ProcessAsync` after work-item-type filtering: resolve subscription, compute `dailyLimit`, count today's runs via `ITestRunRepository.CountTodayAsync`, reject with `QuotaExceeded` when `usedToday >= dailyLimit`; post a plan-specific comment to the Jira issue (quota-exhausted message or no-subscription message) — `source/Testurio.Api/Services/JiraWebhookService.cs`
- [ ] T006 [App] Inject `IQuotaPolicy` and `IUserSubscriptionRepository` into `ADOWebhookService`; add the same quota check in `ProcessAsync` after work-item-type filtering; return `QuotaExceeded` silently (no PM tool comment posted for ADO in v1); log the rejection — `source/Testurio.Api/Services/ADOWebhookService.cs`

### Domain — Test Run Count Query

- [x] T007 [Domain] Add `CountTodayAsync(string userId, DateTimeOffset windowStart, DateTimeOffset windowEnd): Task<int>` to `ITestRunRepository` — `source/Testurio.Core/Repositories/ITestRunRepository.cs`

### Infrastructure — Test Run Count Implementation

- [x] T008 [Infra] Implement `CountTodayAsync` in `TestRunRepository`: query Cosmos `TestRuns` container cross-partition by `userId` and `createdAt` range (windowStart inclusive, windowEnd exclusive); include all statuses including `Skipped` — `source/Testurio.Infrastructure/Cosmos/TestRunRepository.cs`

### Application — Dashboard Quota Fix

- [ ] T009 [App] Inject `IQuotaPolicy` and `IUserSubscriptionRepository` into `DashboardService`; resolve subscription and pass `dailyLimit` from `IQuotaPolicy` to a refactored `GetQuotaUsageAsync` call — `source/Testurio.Api/Services/DashboardService.cs`

### Infrastructure — Stats Repository Quota Signature Update

- [ ] T010 [Infra] Update `StatsRepository.GetQuotaUsageAsync` to accept `int dailyLimit` as a parameter instead of hard-coding `0`; remove the `const int dailyLimit = 0` placeholder — `source/Testurio.Infrastructure/Cosmos/StatsRepository.cs`
- [ ] T011 [Domain] Update `IStatsRepository.GetQuotaUsageAsync` signature to accept `int dailyLimit` parameter — `source/Testurio.Core/Interfaces/IStatsRepository.cs`

### Tests

- [ ] T012 [Test] Unit tests for `QuotaPolicy`: each `SubscriptionPlan` value returns the correct limit; `null` plan returns `0` — `tests/Testurio.UnitTests/Services/QuotaPolicyTests.cs`
- [ ] T013 [Test] Unit tests for `JiraWebhookService` quota path: trigger rejected when `usedToday >= dailyLimit`; quota-exhausted comment posted to Jira; no-subscription comment posted when subscription is `None`; trigger proceeds normally when `usedToday < dailyLimit`; `Trialing` status uses plan-tier limit — `tests/Testurio.UnitTests/Services/JiraWebhookServiceQuotaTests.cs`
- [ ] T014 [Test] Unit tests for `ADOWebhookService` quota path: trigger rejected when quota exhausted; `QuotaExceeded` result returned; no PM tool comment posted — `tests/Testurio.UnitTests/Services/ADOWebhookServiceQuotaTests.cs`
- [ ] T015 [Test] Unit tests for `DashboardService` with quota: `dailyLimit` matches plan-tier value for `Active` user; `dailyLimit` is `0` for `None`/`Expired` user — `tests/Testurio.UnitTests/Services/DashboardServiceQuotaTests.cs`
- [ ] T016 [Test] Integration tests for quota enforcement: Jira webhook returns `200 OK` when quota is exhausted (no retry storm); ADO webhook returns `200 OK` on quota exhaustion; `GET /v1/stats/dashboard` returns correct `dailyLimit` for each `SubscriptionPlan` — `tests/Testurio.IntegrationTests/Controllers/QuotaIntegrationTests.cs`

---

## Rationale

### Task ordering

**`IQuotaPolicy` first (T001).** The interface lives in `Testurio.Core` and is the vocabulary that all downstream layers depend on. It must exist before `QuotaPolicy` (T002), `JiraWebhookService` (T005), `ADOWebhookService` (T006), and `DashboardService` (T009) can compile.

**`QuotaPolicy` and DI registration before webhook services (T002–T003 before T005–T006).** The concrete implementation must be registered in DI before the webhook services that inject it. The DI registration also acts as the validation checkpoint — if the type is missing, startup fails immediately.

**`WebhookProcessResult` enum extended first (T004).** Both `JiraWebhookService` (T005) and `ADOWebhookService` (T006) return `QuotaExceeded`. The enum value must exist before either service is modified.

**`CountTodayAsync` on `ITestRunRepository` before implementation (T007 before T008).** The interface change (T007) drives the contract that `TestRunRepository` (T008) must satisfy. In this project `ITestRunRepository` lives in `Testurio.Core`, so defining the interface first keeps the dependency direction correct (Core has no dependency on Infrastructure).

**Webhook service changes depend on both `IQuotaPolicy` (T001) and `CountTodayAsync` (T007–T008) (T005–T006 after T001 and T008).** The quota check in each webhook service reads today's run count and the daily limit. Both must be available before modifying the service logic.

**Dashboard fix after quota policy (T009–T011 after T001–T003).** `DashboardService` (T009) needs `IQuotaPolicy` to be injectable. The `IStatsRepository` and `StatsRepository` signature change (T011, T010) must happen together so the interface and implementation stay in sync; T009 and T011 are the callers, T010 is the implementation — the interface must change before the callers compile.

**Tests last (T012–T016).** All test tasks follow the implementation tasks that they exercise. Unit tests (T012–T015) mock dependencies and can be written once the public contracts are stable. Integration tests (T016) require a fully wired container and the Cosmos emulator.

### Cross-feature dependencies

- **Feature 0010 (Dashboard):** `QuotaUsage` model, `IStatsRepository.GetQuotaUsageAsync`, and `StatsRepository` are all introduced by feature 0010. Feature 0021 modifies the signature of `GetQuotaUsageAsync` to accept a `dailyLimit` parameter, replacing the hard-coded `0` placeholder that feature 0010 left intentionally. Feature 0010 must be complete before feature 0021 can be implemented.
- **Feature 0015 (Plan Purchase):** `SubscriptionPlan` enum, `SubscriptionStatus` enum, `UserSubscription` entity, and `IUserSubscriptionRepository` are all introduced by feature 0015. Feature 0021 reads the user's subscription to determine the applicable daily limit. Feature 0015 must be complete before feature 0021 can be implemented.
- **Feature 0001 (Automatic Test Run Trigger):** `JiraWebhookService`, `ADOWebhookService`, `ITestRunRepository`, and `WebhookProcessResult` are introduced by feature 0001. Feature 0021 extends these files. Feature 0001 must be complete before feature 0021 can be implemented.
- **Feature 0043 (Dashboard Real-Time Updates):** The SSE stream may carry quota increment events. This is out of scope for feature 0021 — feature 0043 will handle real-time quota counter updates independently.

### Architectural decisions

**No persisted counter.** The daily quota is always computed on the fly by counting `TestRun` documents within the UTC calendar-day window. This eliminates a class of bugs (counter drift, reset failures) at the cost of a cross-partition fan-out query in Cosmos. This is acceptable on the quota check path, which is invoked at most once per webhook delivery — not on the hot read path.

**Soft quota enforcement.** Two simultaneous webhook deliveries near the boundary may both pass the quota check. This is explicitly accepted in US-005 (AC-020 edge case). Atomic enforcement would require distributed locking or a Cosmos stored procedure — unnecessary complexity for v1.

**`IQuotaPolicy` as a Core interface, not an app-layer configuration class.** The interface is placed in `Testurio.Core` so both the webhook services (in `Testurio.Api`) and any future Worker pipeline stages can inject it without a reverse dependency. The `QuotaPolicy` concrete class belongs in `Testurio.Infrastructure` alongside other policy/configuration implementations.

**ADO comment posting deferred.** ADO comment posting is not yet implemented in `ADOWebhookService` (only Jira has this capability in v1). Quota-exhausted ADO triggers are rejected silently (result logged, `200 OK` returned). This avoids blocking the feature on ADO comment infrastructure.

**`GetQuotaUsageAsync` signature change instead of a new method.** Extending the existing method signature with a `dailyLimit` parameter is the minimal change to fulfil AC-020. Adding a second overload or a new repository method would introduce dead code.

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
