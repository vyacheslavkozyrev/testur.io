# Implementation Plan — Configurable API Request Timeout (0022)

## Tasks

- [x] T001 [Domain] Add `RequestTimeoutSeconds` property (default `30`, range enforced by domain constant) to `Project` entity — `source/Testurio.Core/Entities/Project.cs`
- [x] T002 [Domain] Add `ProjectConstants` static class with `RequestTimeoutMinSeconds = 5`, `RequestTimeoutMaxSeconds = 120`, and `RequestTimeoutDefaultSeconds = 30` — `source/Testurio.Core/Constants/ProjectConstants.cs`
- [x] T003 [App] Add `RequestTimeoutSeconds` field (`[Range(5, 120)]`, optional with default 30) to `CreateProjectRequest` DTO — `source/Testurio.Api/DTOs/ProjectDto.cs`
- [x] T004 [App] Add `RequestTimeoutSeconds` field (`[Range(5, 120)]`, optional with default 30) to `UpdateProjectRequest` DTO — `source/Testurio.Api/DTOs/ProjectDto.cs`
- [x] T005 [App] Add `RequestTimeoutSeconds` property to `ProjectDto` response record — `source/Testurio.Api/DTOs/ProjectDto.cs`
- [x] T006 [App] Update `ProjectService.CreateAsync` to write `RequestTimeoutSeconds` (from request, defaulting to `ProjectConstants.RequestTimeoutDefaultSeconds`) to the new `Project` field — `source/Testurio.Api/Services/ProjectService.cs`
- [x] T007 [App] Update `ProjectService.UpdateAsync` to write `RequestTimeoutSeconds` from the update request — `source/Testurio.Api/Services/ProjectService.cs`
- [x] T008 [App] Update `ProjectService.ToDto` to include `RequestTimeoutSeconds` in the mapped `ProjectDto` — `source/Testurio.Api/Services/ProjectService.cs`
- [x] T009 [App] Update `HttpExecutor` to read `ProjectConfig.RequestTimeoutSeconds` and apply it as a per-request `CancellationTokenSource` linked to the run token; on timeout set `Passed: false`, `ErrorMessage: "Timeout — request exceeded {n}s"`, all `AssertionResult.Actual: "<timeout>"`, and record elapsed `DurationMs` — `source/Testurio.Pipeline.Executors/HttpExecutor.cs`
- [x] T010 [App] Update `PlaywrightExecutor` to read `ProjectConfig.RequestTimeoutSeconds`, convert to milliseconds, and pass as Playwright's per-action timeout via `Page.SetDefaultTimeout`; on timeout set step `Passed: false`, `ErrorMessage: "Timeout — action exceeded {n}s"`, and record elapsed `DurationMs`; remaining steps in the scenario are skipped per the existing step-failure logic — `source/Testurio.Pipeline.Executors/PlaywrightExecutor.cs`
- [x] T011 [UI] Add `requestTimeoutSeconds` field to `ProjectDto` and `UpdateProjectRequest` TypeScript types — `source/Testurio.Web/src/types/project.types.ts`
- [x] T012 [UI] Update `projectService` API client to include `requestTimeoutSeconds` in create and update payloads — `source/Testurio.Web/src/services/project/projectService.ts`
- [x] T013 [UI] Update React Query hooks (`useProject`, `useCreateProject`, `useUpdateProject`) to pass and return `requestTimeoutSeconds` — `source/Testurio.Web/src/hooks/useProject.ts`
- [x] T014 [UI] Update MSW mock handlers to include `requestTimeoutSeconds: 30` in mock project responses — `source/Testurio.Web/src/mocks/handlers/project.ts`
- [x] T015 [UI] Add `RequestTimeoutField` component (numeric input, min=5, max=120, step=1, required) and integrate it into the Testing Environment section of `ProjectSettingsPage` alongside the access mode selector from feature 0017 — `source/Testurio.Web/src/components/RequestTimeoutField/RequestTimeoutField.tsx`
- [x] T016 [UI] Add translation keys for the timeout field (label, helper text, validation errors) — `source/Testurio.Web/src/locales/en/project.json`
- [x] T017 [Test] Backend unit tests for `ProjectService`: `CreateAsync` defaults to `30` when `requestTimeoutSeconds` omitted; `UpdateAsync` persists supplied value; `ToDto` maps `RequestTimeoutSeconds` — `tests/Testurio.UnitTests/Services/ProjectServiceTests.cs`
- [x] T018 [Test] Backend unit tests for `HttpExecutor`: request completes within timeout → `DurationMs` recorded, assertions evaluated normally; request exceeds timeout → `Passed: false`, `ErrorMessage` contains `"Timeout"`, all assertions `Actual: "<timeout>"`, subsequent scenarios still execute — `tests/Testurio.UnitTests/Pipeline/Executors/HttpExecutorTests.cs`
- [x] T019 [Test] Backend unit tests for `PlaywrightExecutor`: step completes within timeout → normal evaluation; step exceeds timeout → `Passed: false`, `ErrorMessage` contains `"Timeout"`, remaining steps in scenario skipped, next scenario still executes — `tests/Testurio.UnitTests/Pipeline/Executors/PlaywrightExecutorTests.cs`
- [x] T020 [Test] Backend integration tests for `PUT /v1/projects/{projectId}`: with `requestTimeoutSeconds: 60` → `200 OK` and persisted value; with `requestTimeoutSeconds: 4` → `400 Bad Request`; with `requestTimeoutSeconds: 121` → `400 Bad Request`; omitted field → response includes `30` — `tests/Testurio.IntegrationTests/Controllers/ProjectControllerTests.cs`
- [ ] T021 [Test] Frontend component tests for `RequestTimeoutField`: renders with pre-filled value; shows validation error for values outside 5–120; shows validation error when empty; submitting valid value calls update hook — `source/Testurio.Web/src/components/RequestTimeoutField/RequestTimeoutField.test.tsx`

## Rationale

**Domain first.** T001 adds `RequestTimeoutSeconds` to the `Project` entity in `Testurio.Core`. This field is the single source of truth for all downstream layers — the API service, the executor pipeline, and the UI all derive their behaviour from it. T002 extracts the range boundaries and default into a constants class so they are defined once and referenced everywhere; hardcoding `5`, `120`, and `30` in multiple files would create a maintenance hazard.

**DTOs and service before endpoints.** The existing `PUT /v1/projects/{projectId}` endpoint already handles project updates — no new endpoint is required. T003–T005 extend the three existing DTO types (`CreateProjectRequest`, `UpdateProjectRequest`, `ProjectDto`) to carry the new field. T006–T008 update `ProjectService` to read the field from incoming requests and include it in mapped response DTOs. The endpoint layer (`ProjectEndpoints.cs`) requires no changes because it delegates entirely to `ProjectService` and the `ValidationFilter` automatically validates the new `[Range]` annotation.

**Pipeline executors after domain and service.** T009 (HttpExecutor) and T010 (PlaywrightExecutor) are ordered after the domain and API layers because they depend on `ProjectConfig.RequestTimeoutSeconds` being a stable, well-defined field. Both executors are backend pipeline concerns with no dependency on each other; they are ordered sequentially here (HTTP before Playwright) only for clarity — they can be implemented in parallel if needed.

**`HttpExecutor` timeout strategy.** A `CancellationTokenSource` linked to the outer `CancellationToken` is used rather than setting `HttpClient.Timeout` globally on the shared client, because the timeout must be per-request and must not interfere with other concurrent requests sharing the same `HttpClient` instance. This approach is idiomatic in .NET and avoids the global-client mutation pitfall.

**`PlaywrightExecutor` timeout strategy.** `Page.SetDefaultTimeout(ms)` applies the timeout to all subsequent Playwright actions on that page instance. Because each scenario runs in its own browser context (established in 0029), calling `SetDefaultTimeout` once per scenario context is clean and safe. The timeout fires as a `TimeoutException` from Playwright, which is caught and translated to the step failure shape defined in US-004.

**UI layer follows canonical order.** T011–T016 follow the order mandated by `ui.md`: types → service → hook → mock → component → i18n. No new route is required (the field appears on the existing project settings page). The component is a small standalone `RequestTimeoutField` rather than an inline addition to `ProjectSettingsPage` directly, keeping the settings page composable and the field independently testable.

**No new API endpoint or route registration.** The feature piggybacks on the existing `PUT /v1/projects/{projectId}` endpoint. Adding `requestTimeoutSeconds` to `UpdateProjectRequest` automatically includes it in validation (via the existing `ValidationFilter<UpdateProjectRequest>`) and in the Cosmos document update (via `ProjectService.UpdateAsync`). This is consistent with how `customPrompt` was added in feature 0008.

**Cross-feature dependencies:**

- **Depends on 0006 (Project Creation & Core Configuration):** `Project` entity, `ProjectRepository`, `ProjectService`, `ProjectEndpoints`, `CreateProjectRequest`, `UpdateProjectRequest`, and `ProjectDto` are all implemented. This feature extends each one minimally — no existing behaviour changes.
- **Depends on 0029 (Executor Router):** `HttpExecutor` and `PlaywrightExecutor` must exist before T009 and T010 can extend them. The executor `ProjectConfig` parameter must already carry `RequestTimeoutSeconds` as a field (which comes from the `Project` entity updated in T001).
- **Blocks nothing explicitly, but completes the timeout stub referenced in 0003 and 0029:** Feature 0003 (Automated API Test Execution) and 0029 (Executor Router) both note that per-request configurable timeout is deferred to this feature; their hardcoded or default-timeout implementations are replaced by T009 and T010.

**No Cosmos DB migration.** Cosmos DB is schema-less; adding `RequestTimeoutSeconds` to the `Project` document is a non-breaking, additive change. Existing documents that lack the field are handled by the `?? ProjectConstants.RequestTimeoutDefaultSeconds` fallback in `ProjectService` (AC-009).

**Tests last, per QA rules.** Unit tests (T017–T019) cover the service mapping default/explicit values and the executor timeout-vs-success branching without live HTTP or browser calls. Integration tests (T020) exercise the full validation pipeline through the API. Frontend component tests (T021) confirm field rendering, validation, and hook wiring.

## Layer Tags

| Tag | Scope |
|-----|-------|
| `[Domain]` | Entities, constants — `Testurio.Core` |
| `[App]` | DTOs, services, executor extensions — `Testurio.Api`, `Testurio.Pipeline.Executors` |
| `[UI]` | Types, API clients, hooks, MSW handlers, components, i18n translation keys — `Testurio.Web` |
| `[Test]` | Unit, integration, and frontend component test files — `tests/`, co-located component tests |
