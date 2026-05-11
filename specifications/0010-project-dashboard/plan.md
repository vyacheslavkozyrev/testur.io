# Implementation Plan — Project Dashboard (0010)

## Tasks

- [ ] T001 [Domain] Extend `RunStatus` enum to include all seven values: `Queued`, `Running`, `Passed`, `Failed`, `Cancelled`, `TimedOut`, `NeverRun` — `source/Testurio.Core/Enums/RunStatus.cs`
- [ ] T002 [Domain] Create `LatestRunSummary` value object (`runId`, `status`, `startedAt`, `completedAt`) — `source/Testurio.Core/Models/LatestRunSummary.cs`
- [ ] T003 [Domain] Create `DashboardProjectSummary` value object (`projectId`, `name`, `productUrl`, `testingStrategy`, `latestRun` nullable `LatestRunSummary`) — `source/Testurio.Core/Models/DashboardProjectSummary.cs`
- [ ] T004 [Domain] Create `QuotaUsage` value object (`usedToday`, `dailyLimit`, `resetsAt`) — `source/Testurio.Core/Models/QuotaUsage.cs`
- [ ] T005 [Domain] Create `DashboardUpdatedEvent` record (`projectId`, `latestRun`, optional `quotaUsage`) — `source/Testurio.Core/Events/DashboardUpdatedEvent.cs`
- [ ] T006 [Domain] Add `IStatsRepository` interface with methods `GetDashboardSummariesAsync` and `GetQuotaUsageAsync` — `source/Testurio.Core/Interfaces/IStatsRepository.cs`
- [ ] T007 [Domain] Add `IDashboardStreamManager` interface with methods `PublishAsync(userId, DashboardUpdatedEvent)` and `StreamAsync(userId, CancellationToken)` — `source/Testurio.Core/Interfaces/IDashboardStreamManager.cs`
- [ ] T008 [Infra] Implement `StatsRepository`: query Cosmos `Projects` container for active projects by `userId`, join each with its most recent entry from `TestResults` by `startedAt` descending; projects with no results sort after those with results (alphabetically by name) — `source/Testurio.Infrastructure/Cosmos/StatsRepository.cs`
- [ ] T009 [Infra] Implement `DashboardStreamManager`: holds per-userId `Channel<DashboardUpdatedEvent>` instances; `PublishAsync` writes to the matching channel; `StreamAsync` reads from it and yields SSE-formatted lines — `source/Testurio.Infrastructure/Sse/DashboardStreamManager.cs`
- [ ] T010 [Infra] Register `StatsRepository` and `DashboardStreamManager` as singletons in DI — `source/Testurio.Infrastructure/DependencyInjection.cs`
- [ ] T011 [App] Create `DashboardResponse` DTO (`projects: DashboardProjectSummary[]`, `quotaUsage: QuotaUsage`) — `source/Testurio.Api/Dtos/DashboardResponse.cs`
- [ ] T012 [App] Implement `DashboardService`: call `IStatsRepository.GetDashboardSummariesAsync` (sorted server-side — runs present first by `startedAt` desc, then no-run projects alpha), call `GetQuotaUsageAsync`, assemble `DashboardResponse` — `source/Testurio.Api/Services/DashboardService.cs`
- [ ] T013 [App] Implement `DashboardEventRelay` hosted service: subscribes to the Service Bus topic for run-status-changed messages; for each message deserialises a `DashboardUpdatedEvent` and calls `IDashboardStreamManager.PublishAsync(userId, event)` to fan out to open SSE connections — `source/Testurio.Api/Services/DashboardEventRelay.cs`
- [ ] T014 [API] Register stats route group; add `GET /v1/stats/dashboard` endpoint: requires auth, extracts `userId` from JWT, delegates to `DashboardService`, returns `TypedResults.Ok(DashboardResponse)` — `source/Testurio.Api/Endpoints/StatsEndpoints.cs`
- [ ] T015 [API] Add `GET /v1/stats/dashboard/stream` SSE endpoint in `StatsEndpoints.cs`: requires auth, sets `Content-Type: text/event-stream`, calls `IDashboardStreamManager.StreamAsync(userId, ct)` and writes each `DashboardUpdatedEvent` as a `data:` line — `source/Testurio.Api/Endpoints/StatsEndpoints.cs`
- [ ] T016 [UI] Add TypeScript types: `RunStatus` union of all 7 literals, `LatestRunSummary`, `DashboardProjectSummary`, `QuotaUsage`, `DashboardResponse`, `DashboardUpdatedEvent` — `source/Testurio.Web/src/types/dashboard.types.ts`
- [ ] T017 [UI] Add dashboard API service: `get()` calls `GET /v1/stats/dashboard` — `source/Testurio.Web/src/services/dashboard/dashboardService.ts`
- [ ] T018 [UI] Add `useDashboard` React Query hook (query key `['dashboard']`, calls `dashboardService.get()`) — `source/Testurio.Web/src/hooks/useDashboard.ts`
- [ ] T019 [UI] Add `useDashboardStream` custom hook: opens `EventSource` to `/v1/stats/dashboard/stream` after snapshot is loaded; on `message` event parses `DashboardUpdatedEvent` and calls a provided `onUpdate` callback; implements exponential back-off reconnect (initial 1 s, max 30 s, max 5 attempts); closes `EventSource` on unmount — `source/Testurio.Web/src/hooks/useDashboardStream.ts`
- [ ] T020 [UI] Add MSW mock handler for `GET /v1/stats/dashboard` (returns populated mock response) and a mock SSE handler for `GET /v1/stats/dashboard/stream` — `source/Testurio.Web/src/mocks/handlers/dashboard.ts`
- [ ] T021 [UI] Create `RunStatusBadge` component: accepts `status: RunStatus`; maps each of the 7 values to an MUI `Chip` with appropriate `color` and label; `running` status renders with a CSS pulse animation — `source/Testurio.Web/src/components/RunStatusBadge/RunStatusBadge.tsx`
- [ ] T022 [UI] Create `QuotaUsageBar` component: accepts `quotaUsage: QuotaUsage`; renders global bar above card grid; amber when `usedToday === dailyLimit`, red when `usedToday > dailyLimit`, "No active plan" when `dailyLimit === 0` — `source/Testurio.Web/src/components/QuotaUsageBar/QuotaUsageBar.tsx`
- [ ] T023 [UI] Create `ProjectCard` component: accepts `project: DashboardProjectSummary`; renders card fields and `RunStatusBadge`; entire card surface is wrapped in a Next.js `Link` to `/projects/:id/history`; uses the `PROJECT_HISTORY_ROUTE` builder from `routes.tsx` — `source/Testurio.Web/src/components/ProjectCard/ProjectCard.tsx`
- [ ] T024 [UI] Create `DashboardPage` page component: renders `QuotaUsageBar` at top, card grid using `ProjectCard`, loading skeleton, empty state panel with "Create your first project" CTA, error state with "Retry"; integrates `useDashboard` and `useDashboardStream`; on `DashboardUpdatedEvent` received updates the affected project in local state and optionally refreshes quota — `source/Testurio.Web/src/pages/DashboardPage/DashboardPage.tsx`
- [ ] T025 [UI] Add dashboard translation keys — `source/Testurio.Web/src/locales/en/dashboard.json`
- [ ] T026 [UI] Register routes in `routes.tsx`: `/dashboard` mapped to `DashboardPage`; export `PROJECT_HISTORY_ROUTE = (id: string) => /projects/${id}/history` and `PROJECT_SETTINGS_ROUTE = (id: string) => /projects/${id}/settings` as named constants — `source/Testurio.Web/src/routes/routes.tsx`
- [ ] T027 [Test] Backend unit tests for `DashboardService`: projects with runs (correct sort order), projects without runs (alpha sort, appear last), mixed list, quota at limit, quota with `dailyLimit: 0` — `tests/Testurio.UnitTests/Services/DashboardServiceTests.cs`
- [ ] T028 [Test] Backend unit tests for `DashboardStreamManager`: publish routes to the correct channel, unknown userId creates channel, StreamAsync yields events in order — `tests/Testurio.UnitTests/Services/DashboardStreamManagerTests.cs`
- [ ] T029 [Test] Backend integration tests for `GET /v1/stats/dashboard`: auth required (401 on missing token), cross-tenant isolation, soft-deleted projects excluded, empty array for new user, sort order verified — `tests/Testurio.IntegrationTests/Controllers/StatsControllerTests.cs`
- [ ] T030 [Test] Backend integration tests for `GET /v1/stats/dashboard/stream`: auth required (401 on missing token), first SSE event received after `PublishAsync`, connection closes on cancellation — `tests/Testurio.IntegrationTests/Controllers/StatsControllerTests.cs`
- [ ] T031 [Test] Frontend component tests for `RunStatusBadge`: renders correct MUI Chip label and color for all 7 `RunStatus` values — `source/Testurio.Web/src/components/RunStatusBadge/RunStatusBadge.test.tsx`
- [ ] T032 [Test] Frontend component tests for `ProjectCard`: card link points to correct history URL, `RunStatusBadge` rendered with correct status, `never_run` shows no timestamp — `source/Testurio.Web/src/components/ProjectCard/ProjectCard.test.tsx`
- [ ] T033 [Test] Frontend component tests for `QuotaUsageBar`: numeric ratio shown, amber at limit, red over limit, "No active plan" when `dailyLimit: 0` — `source/Testurio.Web/src/components/QuotaUsageBar/QuotaUsageBar.test.tsx`
- [ ] T034 [Test] Frontend component tests for `DashboardPage`: loading skeleton visible during fetch, empty state shown on empty projects, error state with retry, card grid renders on success, `useDashboardStream` callback updates card badge in place — `source/Testurio.Web/src/pages/DashboardPage/DashboardPage.test.tsx`
- [ ] T035 [Test] E2E tests for dashboard: authenticated user sees card grid sorted correctly, empty state CTA visible for new user, card click navigates to `/projects/:id/history`, quota bar present at top of page — `source/Testurio.Web/e2e/dashboard.spec.ts`

## Rationale

**Domain layer first (T001–T007).** The `RunStatus` enum (T001) is extended before any value objects are created because `LatestRunSummary` (T002) depends on it. The seven-value enum is the single source of truth for both the backend serialisation and the frontend `RunStatus` TypeScript union. `DashboardProjectSummary` (T003) depends on `LatestRunSummary`, so it follows. `QuotaUsage` (T004) and `DashboardUpdatedEvent` (T005) are independent value objects that are defined before the interfaces that reference them. `IStatsRepository` (T006) and `IDashboardStreamManager` (T007) are interfaces depending on the value objects above — they must be finalised before infrastructure implementations are written.

**Infrastructure before application layer (T008–T010 before T011–T013).** `StatsRepository` (T008) implements the Cosmos queries; `DashboardStreamManager` (T009) implements the SSE fan-out mechanism using `Channel<T>`. Both must exist before `DashboardService` (T012) and `DashboardEventRelay` (T013) can be injected with them. DI registration (T010) happens immediately after both implementations are complete so that integration tests can exercise a fully wired container.

**`DashboardEventRelay` as a hosted service (T013).** The Service Bus → SSE fan-out path is implemented as a background `IHostedService` inside `Testurio.Api` rather than in the Worker. The Worker already publishes run-status-changed messages to Service Bus as part of the test pipeline (feature 0001). The relay consumes those messages and calls `IDashboardStreamManager.PublishAsync`, which writes to the appropriate user's channel. This keeps the Worker's responsibilities limited to test execution and result writing, and places the real-time delivery concern in the API process where SSE connections are held.

**API endpoints after the service layer (T014–T015 after T012–T013).** `StatsEndpoints.cs` is intentionally written last among backend tasks because it is the thinnest layer — it extracts `userId`, delegates to `DashboardService`, and streams from `DashboardStreamManager`. Both delegates must compile before the endpoint file is complete. The SSE endpoint (T015) is added to the same `StatsEndpoints.cs` file to keep the stats route group cohesive.

**Frontend follows the canonical layer order (T016–T026).** TypeScript types (T016) are defined first so the service (T017) and hooks (T018–T019) compile against them. MSW handlers (T020) are added before any component so tests can exercise hooks in isolation. Components are built in leaf-to-composite order: `RunStatusBadge` (T021) is a leaf used by `ProjectCard` (T023); `QuotaUsageBar` (T022) is a sibling leaf. Both are composed into `DashboardPage` (T024). Translation keys (T025) and route registration (T026) are last because they depend on the final component tree and page structure being stable.

**`useDashboardStream` as a dedicated hook (T019).** SSE connection lifecycle (open, reconnect with back-off, close on unmount, fallback to re-fetch) is non-trivial and must be isolated from `DashboardPage` to be testable in isolation. The hook accepts an `onUpdate` callback so that `DashboardPage` controls state mutations and the hook remains stateless.

**Route constants exported from `routes.tsx` (T026).** `PROJECT_HISTORY_ROUTE` is defined here because feature 0010 owns the card navigation behaviour (US-005). `PROJECT_SETTINGS_ROUTE` is also defined here (US-006) as a navigation contract for feature 0011. Neither constant registers a page component — they are builder functions `(id: string) => string`. Feature 0011 imports `PROJECT_HISTORY_ROUTE` to register its page; feature 0006 (or its amendment) registers the settings page at the path `PROJECT_SETTINGS_ROUTE` produces.

**`RunStatusBadge` as a reusable component (T021).** The badge is extracted as an independent component because feature 0011 (per-project test history) will display individual run statuses using the same seven-value mapping. Defining and testing it here avoids duplication when feature 0011 is implemented.

**Tests last (T027–T035).** Backend unit tests (T027–T028) mock `IStatsRepository` and `IDashboardStreamManager` respectively — both interfaces must be defined before tests can be written. Integration tests (T029–T030) require a fully wired container and an in-memory Cosmos emulator plus Service Bus test double. Frontend component tests (T031–T034) rely on MSW handlers added in T020. E2E tests (T035) require the full Next.js dev server and backend stubs.

**Cross-feature dependencies.**

- **Feature 0001**: The Worker already publishes run-status-changed messages to Service Bus. `DashboardEventRelay` (T013) consumes the same topic. No changes to the Worker are required; the message contract must be verified to include `userId`, `projectId`, and `RunStatus`.
- **Feature 0006**: `Project` entity fields (`name`, `productUrl`, `testingStrategy`, `isDeleted`) are already defined. `StatsRepository` reads them directly; no schema changes are needed.
- **Feature 0011**: Imports `PROJECT_HISTORY_ROUTE` from this feature's `routes.tsx` and registers its page component at that path. Feature 0011 also imports `RunStatusBadge` from T021 — it must not create a duplicate component.
- **Feature 0021** (Plan-Tier Quota Enforcement): The `QuotaUsage` model and `usedToday` calculation introduced here establish the data contract that feature 0021 will enforce at trigger time. Feature 0021 must not change the quota semantics without an amendment to this plan.

**No new Cosmos containers.** The dashboard reads from the existing `Projects` and `TestResults` containers (partition key: `userId`). All queries are scoped by `userId` at the SDK level, making cross-tenant reads structurally impossible — consistent with the multi-tenancy model.

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
