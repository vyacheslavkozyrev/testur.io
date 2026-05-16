# Implementation Plan — Project Dashboard — Snapshot (0010)

## Tasks

- [x] T001 [Domain] Extend `RunStatus` enum to include all seven values: `Queued`, `Running`, `Passed`, `Failed`, `Cancelled`, `TimedOut`, `NeverRun` — `source/Testurio.Core/Enums/RunStatus.cs`
- [x] T002 [Domain] Create `LatestRunSummary` value object (`runId`, `status`, `startedAt`, `completedAt`) — `source/Testurio.Core/Models/LatestRunSummary.cs`
- [x] T003 [Domain] Create `DashboardProjectSummary` value object (`projectId`, `name`, `productUrl`, `testingStrategy`, `latestRun` nullable `LatestRunSummary`) — `source/Testurio.Core/Models/DashboardProjectSummary.cs`
- [x] T004 [Domain] Create `QuotaUsage` value object (`usedToday`, `dailyLimit`, `resetsAt`) — `source/Testurio.Core/Models/QuotaUsage.cs`
- [x] T005 [Domain] Create `DashboardUpdatedEvent` record (`projectId`, `latestRun`, optional `quotaUsage`) — `source/Testurio.Core/Events/DashboardUpdatedEvent.cs`; defined here so feature 0043 can reference it without a reverse dependency
- [x] T006 [Domain] Add `IStatsRepository` interface with methods `GetDashboardSummariesAsync` and `GetQuotaUsageAsync` — `source/Testurio.Core/Interfaces/IStatsRepository.cs`
- [x] T007 [Infra] Implement `StatsRepository`: query Cosmos `Projects` container for active projects by `userId`, join each with its most recent entry from `TestResults` by `startedAt` descending; projects with no results sort after those with results (alphabetically by name) — `source/Testurio.Infrastructure/Cosmos/StatsRepository.cs`
- [x] T008 [Infra] Register `StatsRepository` as a scoped service in DI — `source/Testurio.Infrastructure/DependencyInjection.cs`; `IDashboardStreamManager` registration is handled by feature 0043
- [x] T009 [App] Create `DashboardResponse` DTO (`projects: DashboardProjectSummary[]`, `quotaUsage: QuotaUsage`) — `source/Testurio.Api/Dtos/DashboardResponse.cs`
- [x] T010 [App] Implement `DashboardService`: call `IStatsRepository.GetDashboardSummariesAsync` (sorted server-side — runs present first by `startedAt` desc, then no-run projects alpha), call `GetQuotaUsageAsync`, assemble `DashboardResponse` — `source/Testurio.Api/Services/DashboardService.cs`
- [x] T011 [API] Register stats route group; add `GET /v1/stats/dashboard` endpoint: requires auth, extracts `userId` from JWT, delegates to `DashboardService`, returns `TypedResults.Ok(DashboardResponse)` — `source/Testurio.Api/Endpoints/StatsEndpoints.cs`
- [x] T012 [UI] Add TypeScript types: `RunStatus` union of all 7 literals, `LatestRunSummary`, `DashboardProjectSummary`, `QuotaUsage`, `DashboardResponse`, `DashboardUpdatedEvent` — `source/Testurio.Web/src/types/dashboard.types.ts`; `DashboardUpdatedEvent` is defined here so feature 0043's `useDashboardStream` hook can import it
- [x] T013 [UI] Add dashboard API service: `get()` calls `GET /v1/stats/dashboard` — `source/Testurio.Web/src/services/dashboard/dashboardService.ts`
- [x] T014 [UI] Add `useDashboard` React Query hook (query key `['dashboard']`, calls `dashboardService.get()`) — `source/Testurio.Web/src/hooks/useDashboard.ts`
- [x] T015 [UI] Add MSW mock handler for `GET /v1/stats/dashboard` (returns populated mock response including `quotaUsage`) — `source/Testurio.Web/src/mocks/handlers/dashboard.ts`; the SSE stream mock handler is added by feature 0043
- [x] T016 [UI] Create `RunStatusBadge` component: accepts `status: RunStatus`; maps each of the 7 values to an MUI `Chip` with appropriate `color` and label; `running` status renders with a CSS pulse animation — `source/Testurio.Web/src/components/RunStatusBadge/RunStatusBadge.tsx`; this component is reused by feature 0011
- [x] T017 [UI] Create `QuotaUsageBar` component: accepts `quotaUsage: QuotaUsage`; renders global bar above card grid; amber when `usedToday === dailyLimit`, red when `usedToday > dailyLimit`, "No active plan" when `dailyLimit === 0` — `source/Testurio.Web/src/components/QuotaUsageBar/QuotaUsageBar.tsx`
- [x] T018 [UI] Create `ProjectCard` component: accepts `project: DashboardProjectSummary`; renders card fields and `RunStatusBadge`; entire card surface is wrapped in a Next.js `Link` to `/projects/:id/history`; uses the `PROJECT_HISTORY_ROUTE` builder from `routes.tsx` — `source/Testurio.Web/src/components/ProjectCard/ProjectCard.tsx`
- [x] T019 [UI] Create `DashboardPage` page component: renders `QuotaUsageBar` at top, card grid using `ProjectCard`, loading skeleton, empty state panel with "Create your first project" CTA, error state with "Retry"; integrates `useDashboard`; accepts an optional `onStreamUpdate` prop (no-op in this feature, wired by feature 0043) — `source/Testurio.Web/src/views/DashboardPage/DashboardPage.tsx`
- [x] T020 [UI] Add dashboard translation keys — `source/Testurio.Web/src/locales/en/dashboard.json`; reconnecting and live-updates-unavailable strings are added by feature 0043
- [x] T021 [UI] Register routes in `routes.tsx`: `/dashboard` mapped to `DashboardPage`; export `PROJECT_HISTORY_ROUTE = (id: string) => \`/projects/${id}/history\`` and `PROJECT_SETTINGS_ROUTE = (id: string) => \`/projects/${id}/settings\`` as named constants — `source/Testurio.Web/src/routes/routes.tsx`
- [x] T022 [Test] Backend unit tests for `DashboardService`: projects with runs (correct sort order), projects without runs (alpha sort, appear last), mixed list, quota at limit, quota with `dailyLimit: 0` — `tests/Testurio.UnitTests/Services/DashboardServiceTests.cs`
- [x] T023 [Test] Backend integration tests for `GET /v1/stats/dashboard`: auth required (401 on missing token), cross-tenant isolation, soft-deleted projects excluded, empty array for new user, sort order verified — `tests/Testurio.IntegrationTests/Controllers/StatsControllerTests.cs`
- [x] T024 [Test] Frontend component tests for `RunStatusBadge`: renders correct MUI Chip label and color for all 7 `RunStatus` values — `source/Testurio.Web/src/components/RunStatusBadge/RunStatusBadge.test.tsx`
- [x] T025 [Test] Frontend component tests for `ProjectCard`: card link points to correct history URL, `RunStatusBadge` rendered with correct status, `never_run` shows no timestamp — `source/Testurio.Web/src/components/ProjectCard/ProjectCard.test.tsx`
- [x] T026 [Test] Frontend component tests for `QuotaUsageBar`: numeric ratio shown, amber at limit, red over limit, "No active plan" when `dailyLimit: 0` — `source/Testurio.Web/src/components/QuotaUsageBar/QuotaUsageBar.test.tsx`
- [ ] T027 [Test] Frontend component tests for `DashboardPage`: loading skeleton visible during fetch, empty state shown on empty projects, error state with retry, card grid renders on success — `source/Testurio.Web/src/pages/DashboardPage/DashboardPage.test.tsx`
- [ ] T028 [Test] E2E tests for dashboard: authenticated user sees card grid sorted correctly, empty state CTA visible for new user, card click navigates to `/projects/:id/history`, quota bar present at top of page — `source/Testurio.Web/e2e/dashboard.spec.ts`

## Rationale

**Domain layer first (T001–T006).** The `RunStatus` enum (T001) is extended before any value objects are created because `LatestRunSummary` (T002) depends on it. `DashboardProjectSummary` (T003) depends on `LatestRunSummary`. `QuotaUsage` (T004) and `DashboardUpdatedEvent` (T005) are independent value objects. `DashboardUpdatedEvent` is defined here, not in feature 0043, so the frontend types file (T012) can include it and feature 0043's `useDashboardStream` hook can import it without a reverse dependency. `IStatsRepository` (T006) is an interface depending on the above value objects and must be finalised before the infrastructure implementation.

**`IDashboardStreamManager` is not defined in this feature.** The interface and its implementation (`DashboardStreamManager`) belong entirely to feature 0043, which owns the SSE concern. This feature's DI registration (T008) only registers `StatsRepository`.

**Infrastructure before application layer (T007–T008 before T009–T010).** `StatsRepository` (T007) implements the Cosmos queries. It must exist before `DashboardService` (T010) can be injected with it. DI registration (T008) happens immediately after so integration tests can exercise a fully wired container.

**API endpoint after the service layer (T011 after T010).** `StatsEndpoints.cs` is the thinnest layer — it extracts `userId`, delegates to `DashboardService`, and returns the response. Feature 0043 adds the SSE endpoint to the same `StatsEndpoints.cs` file.

**Frontend follows the canonical layer order (T012–T021).** TypeScript types (T012) are defined first — including `DashboardUpdatedEvent` for future use by feature 0043's hook. The service (T013) and hooks (T014) compile against them. The MSW snapshot handler (T015) is added before any component. Components are built leaf-to-composite: `RunStatusBadge` (T016) and `QuotaUsageBar` (T017) are leaves used by `ProjectCard` (T018) and `DashboardPage` (T019). Translation keys (T020) and route registration (T021) are last.

**`DashboardPage` is intentionally SSE-free here (T019).** Feature 0043 imports and extends this component to wire in `useDashboardStream`. No stub or placeholder for SSE state is needed — the page simply uses `useDashboard` for its data and renders correctly without live updates.

**`RunStatusBadge` as a reusable component (T016).** Extracted as an independent component because feature 0011 (per-project test history) will display individual run statuses using the same seven-value mapping.

**Route constants exported from `routes.tsx` (T021).** `PROJECT_HISTORY_ROUTE` is defined here because feature 0010 owns card navigation (US-004). `PROJECT_SETTINGS_ROUTE` is also defined here (US-005) as a navigation contract for feature 0011.

**Tests last (T022–T028).** Backend unit tests (T022) mock `IStatsRepository`. Integration tests (T023) require a fully wired container and Cosmos emulator. Frontend component tests (T024–T027) rely on the MSW handler added in T015. E2E tests (T028) require the full Next.js dev server. SSE-related tests are entirely in feature 0043.

**Cross-feature dependencies.**

- **Feature 0001**: The Worker publishes run-status-changed messages to Service Bus. Feature 0043 (not this feature) consumes those messages via `DashboardEventRelay`. No changes to the Worker are required for feature 0010.
- **Feature 0006**: `Project` entity fields (`name`, `productUrl`, `testingStrategy`, `isDeleted`) are already defined. `StatsRepository` reads them directly.
- **Feature 0011**: Imports `PROJECT_HISTORY_ROUTE` from this feature's `routes.tsx` and imports `RunStatusBadge` from T016.
- **Feature 0021**: The `QuotaUsage` model and `usedToday` calculation introduced here establish the data contract that feature 0021 will enforce at trigger time.
- **Feature 0043**: Depends on this feature being complete. Adds `IDashboardStreamManager`, `DashboardStreamManager`, `DashboardEventRelay`, the SSE endpoint, and `useDashboardStream`.

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
