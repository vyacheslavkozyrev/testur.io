# Implementation Plan — Automatic Work Item Status Transition After Report Delivery (0024)

## Tasks

- [ ] T001 [Domain] Add `PassedTransitionStatus` and `FailedTransitionStatus` fields to `Project` entity for both Jira and ADO — `source/Testurio.Core/Entities/Project.cs`
- [ ] T002 [Domain] Add `StatusTransitionOutcome` enum (`Succeeded`, `Failed`, `NotConfigured`) — `source/Testurio.Core/Enums/StatusTransitionOutcome.cs`
- [ ] T003 [Domain] Add `StatusTransitionOutcome`, `StatusTransitionError`, and `StatusTransitionedTo` fields to `TestRun` entity — `source/Testurio.Core/Entities/TestRun.cs`
- [ ] T004 [Domain] Add `TransitionIssueStatusAsync` to `IJiraClient` interface (and supporting result record `JiraTransitionResult`) — `source/Testurio.Core/Interfaces/IJiraClient.cs`
- [ ] T005 [Domain] Add `TransitionWorkItemStateAsync` to `IADOClient` interface (and supporting result record `ADOTransitionResult`) — `source/Testurio.Core/Interfaces/IADOClient.cs`
- [ ] T006 [Domain] Define `IWorkItemTransitionService` interface — `source/Testurio.Core/Interfaces/IWorkItemTransitionService.cs`
- [ ] T007 [Infra] Implement `TransitionIssueStatusAsync` on `JiraAdditionalClient` (calls `POST /rest/api/3/issue/{issueKey}/transitions` with status name lookup) — `source/Testurio.Infrastructure/Jira/JiraAdditionalClient.cs`
- [ ] T008 [Infra] Implement `TransitionWorkItemStateAsync` on `ADOClient` (calls `PATCH /_apis/wit/workitems/{id}?api-version=7.1` with `/fields/System.State` JSON patch) — `source/Testurio.Infrastructure/ADO/ADOClient.cs`
- [ ] T009 [App] Implement `WorkItemTransitionService` (resolves PM tool from project, resolves credentials via `KeyVaultCredentialClient`, calls the appropriate client, returns `StatusTransitionOutcome` and error detail) — `source/Testurio.Api/Services/WorkItemTransitionService.cs`
- [ ] T010 [Infra] Register `IWorkItemTransitionService` → `WorkItemTransitionService` in DI — `source/Testurio.Infrastructure/DependencyInjection.cs`
- [ ] T011 [App] Add `PassedTransitionStatus` and `FailedTransitionStatus` optional fields to `SaveJiraConnectionRequest` DTO — `source/Testurio.Api/DTOs/SaveJiraConnectionRequest.cs`
- [ ] T012 [App] Add `PassedTransitionStatus` and `FailedTransitionStatus` optional fields to `SaveADOConnectionRequest` DTO — `source/Testurio.Api/DTOs/SaveADOConnectionRequest.cs`
- [ ] T013 [App] Add `JiraPassedTransitionStatus`, `JiraFailedTransitionStatus`, `AdoPassedTransitionStatus`, `AdoFailedTransitionStatus` to `PMToolConnectionResponse` DTO — `source/Testurio.Api/DTOs/PMToolConnectionResponse.cs`
- [ ] T014 [App] Update `PMToolConnectionService.SaveJiraConnectionAsync` to persist the two new Jira transition status fields on the project document — `source/Testurio.Api/Services/PMToolConnectionService.cs`
- [ ] T015 [App] Update `PMToolConnectionService.SaveADOConnectionAsync` to persist the two new ADO transition state fields on the project document — `source/Testurio.Api/Services/PMToolConnectionService.cs`
- [ ] T016 [App] Update `PMToolConnectionService.GetIntegrationStatusAsync` to include the four transition fields in the response DTO — `source/Testurio.Api/Services/PMToolConnectionService.cs`
- [ ] T017 [App] Add `StatusTransitionOutcome`, `StatusTransitionError`, and `StatusTransitionedTo` to `RunDetailResponse` DTO — `source/Testurio.Api/DTOs/RunDetailResponse.cs`
- [ ] T018 [App] Update `ProjectHistoryService.GetRunDetailAsync` to populate transition fields from `TestRun` — `source/Testurio.Api/Services/ProjectHistoryService.cs`
- [ ] T019 [Worker] Add `WorkItemTransitionStep` (resolves pass/fail outcome from `TestRun.Status`, selects configured target status, calls `IWorkItemTransitionService`, updates `TestRun` transition fields) — `source/Testurio.Worker/Steps/WorkItemTransitionStep.cs`
- [ ] T020 [Worker] Register `WorkItemTransitionStep` and `IWorkItemTransitionService` in worker DI — `source/Testurio.Worker/DependencyInjection.cs`
- [ ] T021 [Worker] Invoke `WorkItemTransitionStep` in `TestRunJobProcessor.RunReportWriterStageAsync` after `IReportWriter.WriteAsync` succeeds — `source/Testurio.Worker/Processors/TestRunJobProcessor.cs`
- [ ] T022 [UI] Extend `pmTool.types.ts` with `jiraPassedTransitionStatus`, `jiraFailedTransitionStatus`, `adoPassedTransitionStatus`, `adoFailedTransitionStatus` on `PMToolConnectionResponse`; add corresponding fields to `SaveJiraConnectionRequest` and `SaveADOConnectionRequest` — `source/Testurio.Web/src/types/pmTool.types.ts`
- [ ] T023 [UI] Add `statusTransitionOutcome`, `statusTransitionError`, `statusTransitionedTo` to run history/detail types — `source/Testurio.Web/src/types/history.types.ts`
- [ ] T024 [UI] Add two optional text fields ("Transition to status on pass" / "on fail") to `JiraConnectionForm` — `source/Testurio.Web/src/components/Integrations/JiraConnectionForm/JiraConnectionForm.tsx`
- [ ] T025 [UI] Add two optional text fields ("Transition to state on pass" / "on fail") to `ADOConnectionForm` — `source/Testurio.Web/src/components/Integrations/ADOConnectionForm/ADOConnectionForm.tsx`
- [ ] T026 [UI] Extend `RunDetailPanel` to show a "Status transition" row displaying `NotConfigured`, `Succeeded — moved to [status]`, or `Failed — [error]` in an appropriate style — `source/Testurio.Web/src/components/RunDetailPanel/RunDetailPanel.tsx`
- [ ] T027 [UI] Update MSW handler for `POST /v1/projects/:id/integrations/jira` and `POST /v1/projects/:id/integrations/ado` to accept and reflect transition status fields — `source/Testurio.Web/src/mocks/handlers/pmTool.ts`
- [ ] T028 [UI] Update MSW handler for run detail to include transition outcome fields — `source/Testurio.Web/src/mocks/handlers/history.ts`
- [ ] T029 [UI] Add transition field labels to `pmTool.json` locale and transition outcome labels to `history.json` locale — `source/Testurio.Web/src/locales/en/pmTool.json`, `source/Testurio.Web/src/locales/en/history.json`
- [ ] T030 [Test] Unit tests for `WorkItemTransitionService` (Jira pass configured → transition succeeds; Jira fail configured → transition fails → outcome recorded; ADO pass configured; not configured → `NotConfigured`; credentials not found) — `tests/Testurio.UnitTests/Services/WorkItemTransitionServiceTests.cs`
- [ ] T031 [Test] Unit tests for `WorkItemTransitionStep` (pass run → calls service with pass status; fail run → calls service with fail status; transition service throws → pipeline continues, outcome is Failed; not configured → skips call) — `tests/Testurio.UnitTests/Pipeline/Steps/WorkItemTransitionStepTests.cs`
- [ ] T032 [Test] Unit tests for `PMToolConnectionService` save/get methods verifying transition fields are persisted and returned — `tests/Testurio.UnitTests/Services/PMToolConnectionServiceTests.cs`
- [ ] T033 [Test] Frontend component tests for `JiraConnectionForm` showing transition fields and wiring them to the save request — `source/Testurio.Web/src/components/Integrations/JiraConnectionForm/JiraConnectionForm.test.tsx`
- [ ] T034 [Test] Frontend component tests for `ADOConnectionForm` showing transition fields — `source/Testurio.Web/src/components/Integrations/ADOConnectionForm/ADOConnectionForm.test.tsx`
- [ ] T035 [Test] Frontend component tests for `RunDetailPanel` rendering all three transition outcomes — `source/Testurio.Web/src/components/RunDetailPanel/RunDetailPanel.test.tsx`

## Rationale

### Layer sequencing

Domain changes precede all other work. Adding fields to `Project` (T001) and `TestRun` (T003), the `StatusTransitionOutcome` enum (T002), and the two interface contracts `IJiraClient.TransitionIssueStatusAsync` / `IADOClient.TransitionWorkItemStateAsync` (T004–T005) and `IWorkItemTransitionService` (T006) must all exist before any implementing code can compile. This mirrors the pattern used in features 0003–0005, where domain contracts are always the first tasks.

### Infrastructure before Application services

`JiraAdditionalClient` and `ADOClient` implement the new interface methods (T007–T008) before `WorkItemTransitionService` (T009) is written, ensuring the service can inject the concrete clients via DI without depending on yet-to-be-compiled types. This matches the [Infra] → [App] ordering rule.

### API-layer DTO and service changes before Worker

The DTO changes (T011–T013) and `PMToolConnectionService` updates (T014–T016) are API-layer concerns that are independent of the worker. They are placed before the worker tasks (T019–T021) purely to keep the API surface stable before pipeline code consumes the same entities. `RunDetailResponse` and `ProjectHistoryService` (T017–T018) are also API concerns grouped here.

### Worker integration last in the backend sequence

`WorkItemTransitionStep` (T019) is the highest-risk change: it adds a new post-report stage that mutates `TestRun` and makes an external PM tool call. Registering and wiring it (T020–T021) after all domain, infra, and application layers are unit-tested means any failure is isolated to the step itself rather than an unresolved dependency. The step is injected into `TestRunJobProcessor.RunReportWriterStageAsync` after the `IReportWriter.WriteAsync` call succeeds, so a transition failure never prevents report delivery.

### UI changes after all backend contracts are stable

Frontend types (T022–T023) are updated before components (T024–T026) to mirror the [UI] implementation order from the project rules (`types → service → hook → mocks → component → page → i18n`). Locale keys (T029) are grouped with mock handler updates (T027–T028) because they are both consumed by the component tests.

### Test tasks last

Backend tests (T030–T032) and frontend tests (T033–T035) are ordered last, consistent with the `[Test]` tag rule — all implementation layers must be in place before test files are written against them.

### Cross-feature dependencies

- **Feature 0004** (Report Delivery to PM Tool) and **feature 0030** (AI-Powered Report Writer) must be implemented before this feature. The transition step fires immediately after `IReportWriter.WriteAsync` completes inside `RunReportWriterStageAsync`. At the time 0024 is implemented, `IReportWriter` is already in place.
- **Feature 0007** (PM Tool Integration) provides the `IJiraClient` / `IADOClient` interfaces and `JiraAdditionalClient` / `ADOClient` implementations that this feature extends with new methods (T004–T005, T007–T008). Those implementations must already exist; this feature adds methods to them rather than creating new infrastructure clients.
- **Feature 0029** (Executor Router) determines whether a run passed or failed via `ExecutionResult` and `TestRun.Status`, which is already written before the transition step runs. No dependency risk.

### Non-invasiveness of transition failures

The transition step wraps its PM tool call in a try/catch (AC-021) and writes the outcome to `TestRun` fields before returning, regardless of the outcome. This keeps the existing `Completed` / `Failed` run statuses unchanged and means the Service Bus message is always completed (not abandoned) whether the transition succeeds or not — preventing an infinite retry loop for a misconfigured status name.

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
