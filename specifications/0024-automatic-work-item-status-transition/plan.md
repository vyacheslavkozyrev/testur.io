# Feature 0024 — Automatic Work Item Status Transition After Report Delivery

## Plan

### [Domain] Core Models and Enums

- [x] T001: Add `JiraPassedTransitionStatus`, `JiraFailedTransitionStatus`, `AdoPassedTransitionStatus`, `AdoFailedTransitionStatus` fields to `Testurio.Core/Entities/Project.cs`
- [x] T002: Create `Testurio.Core/Enums/StatusTransitionOutcome.cs` with `NotConfigured`, `Succeeded`, `Failed`
- [x] T003: Add `StatusTransitionOutcome?`, `StatusTransitionError`, `StatusTransitionedTo` fields to `Testurio.Core/Entities/TestRun.cs`

### [Domain] Interfaces

- [x] T004: Add `JiraTransitionResult` record and `TransitionIssueStatusAsync` method to `Testurio.Core/Interfaces/IJiraClient.cs`
- [x] T005: Add `ADOTransitionResult` record and `TransitionWorkItemStateAsync` method to `Testurio.Core/Interfaces/IADOClient.cs`
- [x] T006: Create `Testurio.Core/Interfaces/IWorkItemTransitionService.cs` with `WorkItemTransitionResult` record and `TransitionAsync` method

### [Infra] Client Implementations

- [x] T007: Implement `TransitionIssueStatusAsync` in `Testurio.Infrastructure/Jira/JiraAdditionalClient.cs` — fetch available transitions via GET, find by name case-insensitively, POST the transition
- [x] T008: Implement `TransitionWorkItemStateAsync` in `Testurio.Infrastructure/ADO/ADOClient.cs` — PATCH with JSON Patch document for `System.State`

### [Infra] WorkItemTransitionService

- [x] T009: Create `Testurio.Infrastructure/WorkItemTransitionService.cs` implementing `IWorkItemTransitionService` — resolve PM tool, retrieve credentials via `ISecretResolver`, call appropriate client
- [x] T010: Register `IWorkItemTransitionService` in `Testurio.Infrastructure/DependencyInjection.cs`

### [API] DTOs and Service Updates

- [x] T011: Add `PassedTransitionStatus` and `FailedTransitionStatus` to `Testurio.Api/DTOs/SaveADOConnectionRequest.cs`
- [x] T012: Add `PassedTransitionStatus` and `FailedTransitionStatus` to `Testurio.Api/DTOs/SaveJiraConnectionRequest.cs`
- [x] T013: Add four transition fields to `Testurio.Api/DTOs/PMToolConnectionResponse.cs`
- [x] T014: Update `PMToolConnectionService.SaveJiraConnectionAsync` to persist Jira transition fields and clear ADO transition fields
- [x] T015: Update `PMToolConnectionService.SaveADOConnectionAsync` to persist ADO transition fields and clear Jira transition fields
- [x] T016: Update `PMToolConnectionService.ToDto` and `RemoveConnectionAsync` to include/clear all four transition fields
- [x] T017: Add `StatusTransitionOutcome`, `StatusTransitionError`, `StatusTransitionedTo` to `Testurio.Api/DTOs/RunDetailResponse.cs`
- [x] T018: Update `ProjectHistoryService.GetRunDetailAsync` to fetch `TestRun` and populate transition fields in `RunDetailResponse`

### [Worker] WorkItemTransitionStep

- [x] T019: Create `Testurio.Worker/Steps/WorkItemTransitionStep.cs` — determine pass/fail from `ExecutionResult`, select target status, call `IWorkItemTransitionService`, update `TestRun` fields, persist
- [x] T020: Register `WorkItemTransitionStep` in `Testurio.Worker/DependencyInjection.cs`
- [x] T021: Update `TestRunJobProcessor` to call `WorkItemTransitionStep.ExecuteAsync` after `ReportWriter` succeeds

### [Frontend] Types

- [x] T022: Add four transition fields to `PMToolConnectionResponse` in `source/Testurio.Web/src/types/pmTool.types.ts`
- [x] T023: Add two optional transition fields to `SaveADOConnectionRequest` and `SaveJiraConnectionRequest` in `pmTool.types.ts`
- [x] T024: Add `StatusTransitionOutcome` type and three fields to `RunDetailResponse` in `source/Testurio.Web/src/types/history.types.ts`

### [Frontend] Components

- [x] T025: Add `passedTransitionStatus` and `failedTransitionStatus` fields to `JiraConnectionForm` — `source/Testurio.Web/src/components/Integrations/JiraConnectionForm/JiraConnectionForm.tsx`
- [x] T026: Add same fields to `ADOConnectionForm` — `source/Testurio.Web/src/components/Integrations/ADOConnectionForm/ADOConnectionForm.tsx`
- [x] T027: Update MSW handler for PM tool integrations to include transition status fields — `source/Testurio.Web/src/mocks/handlers/pmTool.ts`
- [x] T028: Update MSW handler for run detail to include transition outcome fields — `source/Testurio.Web/src/mocks/handlers/history.ts`
- [x] T029: Add transition field labels to `pmTool.json` locale and transition outcome labels to `history.json` locale
- [x] T030: Add status transition outcome row to `RunDetailPanel` — `source/Testurio.Web/src/components/RunDetailPanel/RunDetailPanel.tsx`

### [Test] Unit Tests

- [x] T031: Unit tests for `WorkItemTransitionService` — `tests/Testurio.UnitTests/Services/WorkItemTransitionServiceTests.cs`
- [x] T032: Unit tests for `WorkItemTransitionStep` — `tests/Testurio.UnitTests/Pipeline/Steps/WorkItemTransitionStepTests.cs`
- [x] T033: Unit tests for `PMToolConnectionService` save/get verifying transition fields — `tests/Testurio.UnitTests/Services/PMToolConnectionServiceTests.cs`
- [x] T034: Frontend component tests for `JiraConnectionForm` — `source/Testurio.Web/src/components/Integrations/JiraConnectionForm/JiraConnectionForm.test.tsx`
- [x] T035: Frontend component tests for `ADOConnectionForm` — `source/Testurio.Web/src/components/Integrations/ADOConnectionForm/ADOConnectionForm.test.tsx`
- [x] T036: Frontend component tests for `RunDetailPanel` — `source/Testurio.Web/src/components/RunDetailPanel/RunDetailPanel.test.tsx`
