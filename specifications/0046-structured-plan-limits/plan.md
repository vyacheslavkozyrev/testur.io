# Implementation Plan — Structured Plan Limits & Feature Enforcement (0046)

## Tasks

### Domain layer

- [x] T001 [Domain] Add `PlanLimits` value object (`MaxProjects: int`, `MaxTestRunsPerMonth: int`; `-1` = unlimited) — `source/Testurio.Core/Models/PlanLimits.cs`
- [x] T002 [Domain] Add `PlanFeatures` value object (`ApiTesting: bool`, `UiE2eTesting: bool`, `AiMemory: bool`, `PmReportPostBack: bool`) — `source/Testurio.Core/Models/PlanFeatures.cs`
- [x] T003 [Domain] Extend `PlanDocument` record: add `Limits: PlanLimits` and `Features: PlanFeatures`; rename existing `Features` string list to `DisplayFeatures: IReadOnlyList<string>` — `source/Testurio.Core/Models/PlanDocument.cs`
- [x] T004 [Domain] Add `PlanLimitExceededException` carrying `LimitName: string` and `RequiredPlan: string` — `source/Testurio.Core/Exceptions/PlanLimitExceededException.cs`
- [x] T005 [Domain] Define `IPlanEnforcementService` interface with three methods: `CheckProjectLimitAsync(userId, ct)`, `CheckMonthlyRunQuotaAsync(userId, ct)`, and `GetEffectivePlanAsync(userId, ct) → PlanDocument?` — `source/Testurio.Core/Interfaces/IPlanEnforcementService.cs`
- [x] T006 [Domain] Extend `IPlanRepository`: add `GetByPlanAsync(SubscriptionPlan plan, CancellationToken ct) → Task<PlanDocument?>` alongside existing `ListAllAsync` — `source/Testurio.Core/Repositories/IPlanRepository.cs`
- [x] T007 [Domain] Migrate `QuotaUsage` model from daily to monthly: replace `UsedToday`/`DailyLimit` fields with `UsedThisMonth`/`MonthlyLimit`; change `ResetsAt` semantics to first day of next UTC month (comment update only — no runtime logic in the model) — `source/Testurio.Core/Models/QuotaUsage.cs`

### Infrastructure layer

- [x] T008 [Infra] Implement `GetByPlanAsync` on `PlanRepository`: query Cosmos `Plans` container by `id` matching the plan slug derived from the `SubscriptionPlan` enum (slug map: `TestJunior → "test-junior"`, etc.) — `source/Testurio.Infrastructure/Cosmos/PlanRepository.cs`
- [x] T009 [Infra] Update `StatsRepository.GetQuotaUsageAsync`: replace the daily count (`createdAt >= today midnight UTC`) with a monthly count (`createdAt >= first day of current UTC month`); update the `ResetsAt` computation to first day of next UTC month; read `PlanDocument.Limits.MaxTestRunsPerMonth` instead of a hardcoded daily limit — `source/Testurio.Infrastructure/Cosmos/StatsRepository.cs`
- [x] T010 [Infra] Implement `PlanEnforcementService`: inject `IUserSubscriptionRepository` and `IPlanRepository`; implement `GetEffectivePlanAsync` (returns null when subscription is null/None/Expired), `CheckProjectLimitAsync` (counts non-deleted projects via `IProjectRepository`; throws `PlanLimitExceededException` when count >= `maxProjects` and `maxProjects != -1`), `CheckMonthlyRunQuotaAsync` (counts TestRun documents for current month via `ITestRunRepository`; throws `PlanLimitExceededException` when count >= `maxTestRunsPerMonth` and `maxTestRunsPerMonth != -1`) — `source/Testurio.Infrastructure/Enforcement/PlanEnforcementService.cs`
- [x] T011 [Infra] Register `PlanEnforcementService` as `IPlanEnforcementService` (scoped) in the shared DI registration used by both `Testurio.Api` and `Testurio.Worker` — `source/Testurio.Infrastructure/DependencyInjection.cs`

### Application layer — API

- [x] T012 [App] Update `ProjectService.CreateAsync`: inject `IPlanEnforcementService`; call `CheckProjectLimitAsync` before writing the new project document to Cosmos — `source/Testurio.Api/Services/ProjectService.cs`
- [x] T013 [App] Update `JiraWebhookService` (and `ADOWebhookService` if present): inject `IPlanEnforcementService`; call `CheckMonthlyRunQuotaAsync` before creating the `TestRun` document and publishing to Service Bus — `source/Testurio.Api/Services/JiraWebhookService.cs`
- [x] T014 [App] Register `PlanLimitExceededException` in `GlobalExceptionHandler`: map to `403 Forbidden` with `ProblemDetails` body; set `Title = "Plan limit reached"`; populate `extensions` with `limitName` and `requiredPlan`; format `Detail` for `maxProjects` and `maxTestRunsPerMonth` cases — `source/Testurio.Api/Middleware/GlobalExceptionHandler.cs`
- [x] T015 [App] Update `DashboardService.GetDashboardAsync`: no logic change required — `StatsRepository.GetQuotaUsageAsync` now returns monthly data (T009); the service just passes the result through — `source/Testurio.Api/Services/DashboardService.cs` _(verify only — may require no code change)_
- [x] T016 [App] Update `PlanDefinitionDto` record: add `Limits: PlanLimitsDto` and `Features: PlanFeaturesDto`; rename existing `Features` to `DisplayFeatures`; add `PlanLimitsDto` and `PlanFeaturesDto` nested types in the same file — `source/Testurio.Api/DTOs/Plans/PlanDefinitionDto.cs`
- [x] T017 [App] Update `PlanEndpoints` mapper: map `PlanDocument.Limits` → `PlanLimitsDto` and `PlanDocument.Features` → `PlanFeaturesDto`; map `DisplayFeatures` for the renamed field — `source/Testurio.Api/Endpoints/PlanEndpoints.cs`

### Worker pipeline

- [ ] T018 [App] Add `IMemoryWriterService` interface (Stage 8): `UpsertScenarioAsync(ParsedStory, GeneratorResults, TestRun, CancellationToken)` — `source/Testurio.Core/Interfaces/IMemoryWriterService.cs`
- [ ] T019 [App] Update `TestRunJobProcessor.ExecutePipelineAsync`: inject `IPlanEnforcementService` and `IMemoryWriterService`; after Stage 2 (AgentRouter), call `GetEffectivePlanAsync` once and store the result; (a) filter `resolvedTestTypes` by `features.apiTesting` / `features.uiE2eTesting` flags (AC-034–AC-037); (b) pass `features.aiMemory` to guard Stage 3 (`MemoryRetrieval`) and Stage 8 (`MemoryWriter`); (c) pass `features.pmReportPostBack` to `RunReportWriterStageAsync` — `source/Testurio.Worker/Processors/TestRunJobProcessor.cs`
- [ ] T020 [App] Update `RunReportWriterStageAsync` signature to accept `bool pmReportPostBackEnabled`; pass the flag into `IReportWriter.WriteAsync` so the concrete implementation can skip the PM tool comment post when `false` — `source/Testurio.Worker/Processors/TestRunJobProcessor.cs`
- [ ] T021 [App] Extend `IReportWriter.WriteAsync` signature (or add an overload) to accept `bool postBackEnabled`; in `ReportWriterService`, skip the `ICommentEventSender` call when `postBackEnabled == false` while still persisting the `TestResult` record — `source/Testurio.Core/Interfaces/IReportWriter.cs` + `source/Testurio.Pipeline.ReportWriter/ReportWriterService.cs`
- [ ] T022 [App] Implement `MemoryWriterService` (stub / real): implement `IMemoryWriterService`; for all-pass runs, embed `ParsedStory.Text` via `IEmbeddingService` and upsert to `TestMemory` Cosmos container — `source/Testurio.Pipeline.MemoryWriter/MemoryWriterService.cs`
- [ ] T023 [Infra] Register `MemoryWriterService` as `IMemoryWriterService` (transient) in Worker DI — `source/Testurio.Worker/DependencyInjection.cs`

### Cosmos seed data

- [ ] T024 [Config] Update Cosmos seed / initializer: add `limits` and `features` values to all four plan documents in `CosmosDbInitializer` (or the equivalent seed JSON); rename the `features` array key to `displayFeatures`; do not change pricing values — `source/Testurio.Infrastructure/Cosmos/CosmosDbInitializer.cs`

### Frontend

- [ ] T025 [UI] Update `plan.types.ts`: add `PlanLimits` and `PlanFeatures` interfaces; add `limits: PlanLimits` and `features: PlanFeatures` to `PlanDefinition`; rename `features: string[]` to `displayFeatures: string[]` — `source/Testurio.Web/src/types/plan.types.ts`
- [ ] T026 [UI] Update `dashboard.types.ts`: rename `usedToday → usedThisMonth` and `dailyLimit → monthlyLimit` on the `QuotaUsage` interface; update `resetsAt` JSDoc to reflect monthly reset — `source/Testurio.Web/src/types/dashboard.types.ts`
- [ ] T027 [UI] Update `QuotaUsageBar` component: replace "runs used today" copy with "runs used this month"; replace "Resets at midnight UTC" with "Resets on {formatted date}"; update amber/red threshold logic to use `monthlyLimit`; render "Unlimited" (neutral style) when `monthlyLimit === -1` — `source/Testurio.Web/src/components/QuotaUsageBar/QuotaUsageBar.tsx`
- [ ] T028 [UI] Update MSW mock handler for `GET /v1/plans`: add `limits` and `features` fields to mock plan objects; rename `features` → `displayFeatures` — `source/Testurio.Web/src/mocks/handlers/plan.ts`
- [ ] T029 [UI] Update MSW mock handler for `GET /v1/stats/dashboard`: update `quotaUsage` mock shape from `usedToday`/`dailyLimit` to `usedThisMonth`/`monthlyLimit` — `source/Testurio.Web/src/mocks/handlers/dashboard.ts`
- [ ] T030 [UI] Update `dashboard.json` i18n keys: replace `"runsUsedToday"` / `"resetsAtMidnight"` strings with `"runsUsedThisMonth"` / `"resetsOn"` equivalents; add `"unlimited"` label — `source/Testurio.Web/src/locales/en/dashboard.json`
- [ ] T031 [UI] Update pricing page plan card component to read `displayFeatures` instead of `features` (rename only — no visual change) — `source/Testurio.Web/src/components/PlanCard/PlanCard.tsx` _(or equivalent)_

### Tests

- [ ] T032 [Test] Backend unit tests for `PlanEnforcementService`: `GetEffectivePlanAsync` returns null for no subscription, `CheckProjectLimitAsync` throws at limit, passes below limit, skips when -1, `CheckMonthlyRunQuotaAsync` throws at limit, passes below limit, skips when -1 — `tests/Testurio.UnitTests/Services/PlanEnforcementServiceTests.cs`
- [ ] T033 [Test] Backend unit tests for `ProjectService.CreateAsync`: throws `PlanLimitExceededException` when limit reached (mock enforcement service), proceeds when limit not reached — `tests/Testurio.UnitTests/Services/ProjectServiceTests.cs`
- [ ] T034 [Test] Backend integration tests for project creation enforcement: `POST /v1/projects` returns `403` with correct `ProblemDetails` when project limit is reached — `tests/Testurio.IntegrationTests/Controllers/ProjectControllerTests.cs`
- [ ] T035 [Test] Backend integration tests for webhook quota enforcement: Jira / ADO webhook endpoint returns `403` with correct `ProblemDetails` when monthly run quota is exhausted — `tests/Testurio.IntegrationTests/Controllers/WebhookControllerTests.cs`
- [ ] T036 [Test] Backend unit tests for `StatsRepository.GetQuotaUsageAsync`: monthly count computed correctly, `resetsAt` is first day of next month UTC, `MonthlyLimit` reads from plan — `tests/Testurio.UnitTests/Services/DashboardServiceTests.cs` _(extend existing test class)_
- [ ] T037 [Test] Frontend component tests for `QuotaUsageBar`: monthly copy renders, amber at 80% threshold, red at/over limit, "Unlimited" when `monthlyLimit === -1`, "No active plan" when `monthlyLimit === 0` — `source/Testurio.Web/src/components/QuotaUsageBar/QuotaUsageBar.test.tsx`
- [ ] T038 [Test] E2E tests: quota bar shows monthly copy; project creation shows upgrade prompt when limit reached; webhook trigger returns 403 body on quota exhaustion — `source/Testurio.Web/e2e/plan-limits.spec.ts`

---

## Rationale

### Layer order

**Domain first (T001–T007).** `PlanLimits`, `PlanFeatures`, and `PlanLimitExceededException` are pure value objects and exceptions with no external dependencies — they must exist before any service, repository, or endpoint can reference them. `IPlanEnforcementService` (T005) and the `IPlanRepository` extension (T006) are interfaces defined in `Testurio.Core`, which is the contract boundary consumed by `Testurio.Infrastructure`, `Testurio.Api`, and `Testurio.Worker`. `QuotaUsage` migration (T007) is also domain-layer because it is a model record — it must be updated before the repository implementation (T009) and the frontend types (T026) can be aligned.

**Infrastructure before application (T008–T011 before T012–T023).** `PlanRepository.GetByPlanAsync` (T008) and `StatsRepository.GetQuotaUsageAsync` (T009) must be implemented before `PlanEnforcementService` (T010) can call them. `PlanEnforcementService` must be registered in DI (T011) before the application-layer services (`ProjectService`, webhook services) that inject it can be updated (T012–T013).

**API application layer (T012–T017) before Worker pipeline (T018–T023).** The API enforcement changes (project creation, webhook trigger) are independent of the Worker enforcement changes, but both share the same `IPlanEnforcementService` interface already in place. Worker changes introduce `IMemoryWriterService` (T018), which is new infrastructure; it follows the same domain → infra → app sequence within the Worker scope.

**`IReportWriter` signature change (T021) is carefully ordered.** `IReportWriter` is in `Testurio.Core` but the change affects both the interface and its concrete implementation in `Testurio.Pipeline.ReportWriter`. This is placed after the Worker processor change (T020) so the required signature is known before the interface is modified, avoiding a second pass.

**Cosmos seed (T024) is Config-layer** and placed after all domain and infrastructure code is in place, as it references the final `PlanDocument` structure.

**Frontend (T025–T031) follows backend.** Type updates (T025–T026) must precede component and mock updates because TypeScript components and MSW handlers import from the types file. The `QuotaUsageBar` component (T027) depends on the updated `QuotaUsage` type. Mock handler updates (T028–T029) must be aligned with the new DTO shapes before tests pass. i18n keys (T030) and pricing page rename (T031) are independent of each other and can run in parallel.

**Tests last (T032–T038).** Following the `[Test]` tag rule: unit tests (T032–T033, T036–T037) are written after the services they exercise are implemented. Integration tests (T034–T035) require the full application layer to be complete. The E2E test (T038) requires the UI changes to be deployed.

### Cross-feature dependencies

- **Feature 0010 (Dashboard):** `QuotaUsage` model (T007), `StatsRepository` (T009), `QuotaUsageBar` component (T027), and `dashboard.types.ts` (T026) are all modified. The changes are backward-compatible at the API level by being purely additive on `PlanDefinitionDto` (new `limits`/`features` fields) but breaking on `QuotaUsage` (field rename). Feature 0010's tests are updated in T036–T037 rather than in separate test tasks to avoid duplication.

- **Feature 0021 (Daily Quota):** Feature 0021's enforcement logic (daily run counter) is entirely superseded. The `usedToday` / `dailyLimit` fields are removed from `QuotaUsage` and replaced by monthly equivalents. Feature 0021 is considered retired by this feature; no separate migration is needed because the data model was always computed live from `TestRun` counts.

- **Feature 0027 (MemoryRetrieval) / Feature 0032 (MemoryWriter):** Stage 3 and Stage 8 are now gated by `features.aiMemory`. `IMemoryWriterService` (T018) is the interface stub that will be fully implemented when Feature 0032 is scheduled; this feature introduces it at the Worker layer so the flag gate is functional even before the full implementation arrives.

- **Feature 0030 (ReportWriter):** The `IReportWriter.WriteAsync` signature is extended (T021) to accept the `postBackEnabled` flag. This is a breaking interface change within the solution — all callers must be updated in the same commit. In this feature, the only caller is `TestRunJobProcessor`.

- **Feature 0015 / 0016 (Billing):** The `PlanDocument` structure in Cosmos changes (T024). Existing `GET /v1/plans` consumers receive two new additive fields (`limits`, `features`) and one renamed field (`displayFeatures`). The pricing page component is updated (T031) to use `displayFeatures` — a rename-only change with no visual output difference.

### Architectural decisions

**Enforcement is synchronous and pre-write.** Both project-creation and run-trigger limits are checked before any Cosmos write or Service Bus publish. This ensures that rejected operations leave no partial state (AC-014 requirement). The trade-off is a slightly higher per-request Cosmos read cost, which is acceptable because project creation and webhook handling are low-frequency paths.

**Live Cosmos count rather than a cached counter.** The monthly run count is always read directly from the `TestRun` container (AC-022). A cached counter would require atomic increment logic and a separate document, adding complexity and potential race conditions. At the expected usage volumes (tens to low hundreds of runs per month), a partition-key–scoped count query is fast enough.

**Fail-open for plan resolution in the Worker.** If `GetEffectivePlanAsync` returns null or throws in the Worker pipeline (e.g. Cosmos transient failure), the pipeline treats all feature flags as `true` (AC-026 — fail-open). This avoids silently dropping a test run due to a billing infrastructure blip. The API layer does not fail-open: a missing plan blocks project creation and run triggers because those are explicit user actions where a 403 with a clear message is appropriate.

**`IMemoryWriterService` is introduced as a stub.** Feature 0032 owns the full MemoryWriter implementation. This feature only introduces the interface, the flag gate, and a minimal implementation (or no-op) so the `aiMemory` flag has a real effect. The stub is registered in DI and the Worker pipeline is wired correctly.

## Layer Tags

| Tag | Scope |
|-----|-------|
| `[Domain]` | Entities, interfaces, value objects — `Testurio.Core` |
| `[Infra]` | Repositories, service implementations, DI registration — `Testurio.Infrastructure` |
| `[App]` | DTOs, services, validators, Worker pipeline steps |
| `[API]` | Minimal API endpoints, middleware — `Testurio.Api` |
| `[Config]` | App configuration, seed data, constants |
| `[UI]` | Types, API clients, hooks, MSW handlers, components, pages, i18n keys |
| `[Test]` | Unit, integration, and frontend component test files |
