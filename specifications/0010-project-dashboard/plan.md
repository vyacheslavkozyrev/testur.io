# Implementation Plan — Project Dashboard (0010)

## Tasks

- [ ] T001 [Domain] Add `IStatsRepository` interface for dashboard and quota queries — `source/Testurio.Core/Interfaces/IStatsRepository.cs`
- [ ] T002 [Domain] Create `DashboardProjectSummary` value object (projectId, name, productUrl, testingStrategy, createdAt, latestRun) — `source/Testurio.Core/Models/DashboardProjectSummary.cs`
- [ ] T003 [Domain] Create `LatestRunSummary` value object (runId, status, startedAt, completedAt) — `source/Testurio.Core/Models/LatestRunSummary.cs`
- [ ] T004 [Domain] Create `QuotaUsage` value object (usedToday, dailyLimit, resetsAt) — `source/Testurio.Core/Models/QuotaUsage.cs`
- [ ] T005 [Infra] Implement `StatsRepository` — queries Cosmos for all active projects and joins each with its most recent TestRun by `startedAt` descending — `source/Testurio.Infrastructure/Cosmos/StatsRepository.cs`
- [ ] T006 [Infra] Register `StatsRepository` in DI — `source/Testurio.Infrastructure/DependencyInjection.cs`
- [ ] T007 [App] Create `DashboardResponse` DTO (projects array + quotaUsage) — `source/Testurio.Api/Dtos/DashboardResponse.cs`
- [ ] T008 [App] Implement `DashboardService` (fetch project summaries with latest runs, compute quota usage from subscription plan) — `source/Testurio.Api/Services/DashboardService.cs`
- [ ] T009 [API] Register stats route group and `GET /v1/stats/dashboard` endpoint — `source/Testurio.Api/Endpoints/StatsEndpoints.cs`
- [ ] T010 [UI] Add TypeScript types for dashboard response — `source/Testurio.Web/src/types/dashboard.types.ts`
- [ ] T011 [UI] Add dashboard API service — `source/Testurio.Web/src/services/dashboard/dashboardService.ts`
- [ ] T012 [UI] Add `useDashboard` React Query hook — `source/Testurio.Web/src/hooks/useDashboard.ts`
- [ ] T013 [UI] Add MSW mock handler for dashboard endpoint — `source/Testurio.Web/src/mocks/handlers/dashboard.ts`
- [ ] T014 [UI] Create `RunStatusBadge` component — `source/Testurio.Web/src/components/RunStatusBadge/RunStatusBadge.tsx`
- [ ] T015 [UI] Create `ProjectCard` component — `source/Testurio.Web/src/components/ProjectCard/ProjectCard.tsx`
- [ ] T016 [UI] Create `QuotaUsageIndicator` component — `source/Testurio.Web/src/components/QuotaUsageIndicator/QuotaUsageIndicator.tsx`
- [ ] T017 [UI] Create `DashboardPage` page component with loading skeleton, empty state, and error state — `source/Testurio.Web/src/pages/DashboardPage/DashboardPage.tsx`
- [ ] T018 [UI] Add dashboard translation keys — `source/Testurio.Web/src/locales/en/dashboard.json`
- [ ] T019 [UI] Register `/dashboard` route as the authenticated default landing route — `source/Testurio.Web/src/routes/routes.tsx`
- [ ] T020 [Test] Backend unit tests for `DashboardService` (projects with runs, projects without runs, empty project list, quota calculation) — `tests/Testurio.UnitTests/Services/DashboardServiceTests.cs`
- [ ] T021 [Test] Backend integration tests for `GET /v1/stats/dashboard` (auth required, cross-tenant isolation, soft-deleted projects excluded, empty array) — `tests/Testurio.IntegrationTests/Controllers/StatsControllerTests.cs`
- [ ] T022 [Test] Frontend component tests for `RunStatusBadge` (all status variants) — `source/Testurio.Web/src/components/RunStatusBadge/RunStatusBadge.test.tsx`
- [ ] T023 [Test] Frontend component tests for `ProjectCard` (click navigation, null latestRun, status badge rendering) — `source/Testurio.Web/src/components/ProjectCard/ProjectCard.test.tsx`
- [ ] T024 [Test] Frontend component tests for `DashboardPage` (loading skeleton, empty state, error + retry, populated list) — `source/Testurio.Web/src/pages/DashboardPage/DashboardPage.test.tsx`
- [ ] T025 [Test] E2E tests for dashboard (renders projects, navigates to project settings on card click, empty state CTA) — `source/Testurio.Web/e2e/dashboard.spec.ts`

## Rationale

**Domain layer first (T001–T004).** `IStatsRepository` defines the data access contract for the dashboard. The three value objects (`DashboardProjectSummary`, `LatestRunSummary`, `QuotaUsage`) define the shape of the assembled result before any infrastructure or API code is written. Both depend on types already established by prior features — `TestRun.Status` from feature 0001, `Project` fields from feature 0006 — so no new base entities are needed.

**Infrastructure before application service (T005–T006 before T008).** `StatsRepository` must exist before `DashboardService` can be injected with it. The repository is responsible for the most complex query in this feature: fetching all non-deleted projects for a user and correlating each with its most recent `TestRun` by `startedAt` descending. This query logic is isolated in the repository so `DashboardService` can be unit-tested with a mocked `IStatsRepository`.

**`DashboardService` before the API endpoint (T008 before T009).** The service encapsulates quota calculation (today's run count vs. plan limit, reset timestamp at midnight UTC) and mapping to the response DTO. Keeping this logic in the service layer — not in the endpoint handler — is consistent with the pattern established by features 0006–0009 and keeps the endpoint thin.

**`StatsEndpoints.cs` adds a new route group (T009).** The architecture defines `/api/stats` as a distinct route group. This file registers `GET /v1/stats/dashboard` under that group with `RequireAuthorization()` and extracts `userId` from the JWT via `ClaimsPrincipalExtensions.GetUserId()`, consistent with the security model used across all API endpoints.

**Frontend follows the canonical layer order (T010–T019).** Types are defined first (T010) so the service (T011) and hook (T012) compile against them. The MSW handler (T013) is added before any component so tests can exercise the hook in isolation. Components are built in bottom-up dependency order: `RunStatusBadge` (T014) is a leaf component used by `ProjectCard` (T015); `ProjectCard` and `QuotaUsageIndicator` (T016) are composed into `DashboardPage` (T017). Translation keys (T018) and route registration (T019) are last since they depend on the final component and page structure being stable.

**`RunStatusBadge` as an independent component.** The status badge is extracted into its own component because it will be reused on the per-project test history page (feature 0011). Defining it here, co-located with its tests (T022), avoids duplication when feature 0011 is implemented.

**Tests last (T020–T025).** Backend unit tests (T020) mock `IStatsRepository` and verify quota edge cases (zero limit, no plan). Integration tests (T021) exercise authentication, tenant isolation, and soft-delete exclusion against an in-memory Cosmos emulator. Frontend component tests (T022–T024) use MSW handlers added in T013. E2E tests (T025) require a running Next.js dev server and the full backend stub.

**Cross-feature dependencies.**

- **Feature 0001**: `TestRun` entity and `ITestRunRepository` are already defined. `StatsRepository` queries the same Cosmos container; no changes to `TestRun` are needed.
- **Feature 0006**: `Project` entity and `IProjectRepository` are already defined. The dashboard reads project fields (`name`, `productUrl`, `testingStrategy`, `createdAt`, `isDeleted`) directly; no schema changes are required.
- **Feature 0021** (Plan-Tier Test Run Quota, not yet planned): The `QuotaUsage` model and `usedToday` calculation introduced here establish the data contract that feature 0021 will enforce at trigger time. Feature 0021 must not change the quota counter semantics without an amendment to this plan.
- **Feature 0011** (Per-Project Test History): `RunStatusBadge` (T014) is designed to be reused. Feature 0011 should import it from its location rather than creating a duplicate.

**No new Cosmos containers.** The dashboard aggregates data from the existing `Projects` and `TestResults` containers (partition key: `userId`). All queries are scoped by `userId`, making cross-tenant reads structurally impossible at the SDK level, consistent with the multi-tenancy model.

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
