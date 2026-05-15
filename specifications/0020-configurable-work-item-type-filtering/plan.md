# Implementation Plan — Configurable Work Item Type Filtering (0020)

## Tasks

- [ ] T001 [Domain] Extend `Project` entity with `allowedWorkItemTypes` field and default helper — `source/Testurio.Core/Entities/Project.cs`
- [ ] T002 [Domain] Add `IWorkItemTypeFilterService` interface — `source/Testurio.Core/Interfaces/IWorkItemTypeFilterService.cs`
- [ ] T003 [App] Create `UpdateWorkItemTypeFilterRequest` DTO with validation attributes — `source/Testurio.Api/DTOs/UpdateWorkItemTypeFilterRequest.cs`
- [ ] T004 [App] Implement `WorkItemTypeFilterService` (filter evaluation, default resolution per PM tool type) — `source/Testurio.Api/Services/WorkItemTypeFilterService.cs`
- [ ] T005 [App] Extend `JiraWebhookService` to call `IWorkItemTypeFilterService` and drop filtered events with structured log — `source/Testurio.Api/Services/JiraWebhookService.cs`
- [ ] T006 [App] Create `ADOWebhookService` extension — add issue-type filtering to the ADO webhook handler — `source/Testurio.Api/Services/ADOWebhookService.cs`
- [ ] T007 [API] Add `PATCH /v1/projects/{projectId}/work-item-type-filter` endpoint — `source/Testurio.Api/Endpoints/ProjectEndpoints.cs`
- [ ] T008 [UI] Add `allowedWorkItemTypes` to project TypeScript types — `source/Testurio.Web/src/types/project.types.ts`
- [ ] T009 [UI] Add `updateWorkItemTypeFilter` method to project API service — `source/Testurio.Web/src/services/project/projectService.ts`
- [ ] T010 [UI] Add MSW mock handler for the PATCH filter endpoint — `source/Testurio.Web/src/mocks/handlers/project.ts`
- [ ] T011 [UI] Add `useUpdateWorkItemTypeFilter` mutation hook — `source/Testurio.Web/src/hooks/useProject.ts`
- [ ] T012 [UI] Create `WorkItemTypeFilter` component (multi-select tag input, at-least-one validation, save button) — `source/Testurio.Web/src/components/Integrations/WorkItemTypeFilter/WorkItemTypeFilter.tsx`
- [ ] T013 [UI] Embed `WorkItemTypeFilter` in the Integrations settings page, visible only when PM tool is configured — `source/Testurio.Web/src/pages/ProjectSettings/Integrations/IntegrationPage.tsx`
- [ ] T014 [UI] Add translation keys for the work item type filter section — `source/Testurio.Web/src/locales/en/pmTool.json`
- [ ] T015 [Test] Unit tests for `WorkItemTypeFilterService` (default resolution, pass-through, drop, edge cases) — `tests/Testurio.UnitTests/Services/WorkItemTypeFilterServiceTests.cs`
- [ ] T016 [Test] Unit tests for `JiraWebhookService` filtering behaviour (matching type enqueues, non-matching drops, missing field falls back to default) — `tests/Testurio.UnitTests/Services/JiraWebhookServiceTests.cs`
- [ ] T017 [Test] Integration tests for `PATCH /v1/projects/{projectId}/work-item-type-filter` (valid update, empty array 400, empty string 400, over-limit 400, cross-tenant 403) — `tests/Testurio.IntegrationTests/Controllers/ProjectControllerTests.cs`
- [ ] T018 [Test] Component tests for `WorkItemTypeFilter` (renders current types, adds tag, removes tag, blocks empty save, submits valid list) — `source/Testurio.Web/src/components/Integrations/WorkItemTypeFilter/WorkItemTypeFilter.test.tsx`

## Rationale

### Ordering Principle

Tasks follow the same dependency chain established in features 0006 and 0007: Domain → Application → API → UI → Tests. Each layer depends only on layers completed before it.

### Layer Breakdown

1. **Domain first (T001–T002).** The `Project` entity in `Testurio.Core` must be extended with `allowedWorkItemTypes` before any application or API code can reference it. The `IWorkItemTypeFilterService` interface is defined in Core so it can be injected into webhook services (in `Testurio.Api`) without creating a circular dependency. The interface definition also acts as the stable contract that the unit tests can mock.

2. **Application second (T003–T006).** The DTO and request validation class (T003) defines what the API accepts and surfaces the at-least-one-type and max-20 constraints at the model level — the API endpoint (T007) depends on this. `WorkItemTypeFilterService` (T004) encapsulates the filtering logic (default per PM tool, exact match) independently of transport, making it fully unit-testable. The Jira webhook service extension (T005) and ADO equivalent (T006) consume `IWorkItemTypeFilterService`; they must come after T004 so the concrete implementation exists. ADO comes after Jira (T006 after T005) because the same service interface covers both and the ADO handler is an extension of the same pattern.

3. **API third (T007).** The PATCH endpoint is registered after its DTO (T003), service (T004), and Project entity field (T001) are all in place. It extends the existing `ProjectEndpoints.cs` route group so no new route file is needed.

4. **UI fourth (T008–T014).** UI work proceeds in the canonical order defined by the project rules: types → service → mock → hook → component → page → i18n. The `allowedWorkItemTypes` type addition (T008) extends the already-existing `project.types.ts`. The API service addition (T009) and MSW handler (T010) are independent and could run in parallel but are listed sequentially for clarity. The hook (T011) wraps the service method and depends on both. The component (T012) consumes the hook and renders the multi-select UI. The page integration (T013) conditionally renders the component inside the existing `IntegrationPage.tsx` (established by feature 0007). Translation keys (T014) are added last as they depend on knowing all visible strings.

5. **Tests last (T015–T018).** Unit tests for the filter service (T015) and webhook service filtering behaviour (T016) are independent and could run in parallel but are ordered service-first as the webhook tests build on the filter service behaviour. API integration tests (T017) verify the full request/response contract. Component tests (T018) are last as they depend on the component being complete.

### Cross-Feature Dependencies

**Feature 0006 (Project Creation & Core Configuration)** must be implemented before this feature. `T001` extends the `Project` entity defined in 0006 by adding the `allowedWorkItemTypes` field. The `ProjectRepository` (0006) handles Cosmos DB persistence; no separate repository changes are needed — the field is stored as part of the project document.

**Feature 0007 (PM Tool Integration)** must be implemented before this feature. The `IntegrationPage.tsx` component (0007, T024) is extended in `T013` to embed the `WorkItemTypeFilter` component. The `pmTool` field on the Project entity (added by 0007) determines which default type list to display. The `allowedWorkItemTypes` filter applies to webhook events that 0007's webhook infrastructure handles.

**Feature 0001 (Automatic Test Run Trigger)** is already complete. `T005` extends `JiraWebhookService` (established in 0001, T011) rather than replacing it. The extension adds a filter check before the existing enqueue logic; all existing behaviour is preserved.

**Feature 0024 (Work Item Status Transition)** and **Feature 0019 (Trigger Notification Method)** have no blocking dependency on this feature and are not affected by it.

### Architectural Decisions

1. **Default list encoded per PM tool type.** Rather than requiring QA leads to configure types from scratch on every new project, `WorkItemTypeFilterService.GetDefaultTypes(PMToolType pmTool)` returns `["Story", "Bug"]` for Jira and `["User Story", "Bug"]` for ADO. The `Project` document stores `null` until the QA lead explicitly saves a custom list; `null` is treated as "use default" both in the API response (which returns the effective list) and in the webhook handler (AC-016 fallback).

2. **PATCH rather than PUT.** The filter is a sub-resource of the project document. A dedicated `PATCH /v1/projects/{projectId}/work-item-type-filter` endpoint touches only this field, keeping the payload minimal and avoiding accidental overwrites of unrelated project fields. This follows the pattern used by the 0007 integration endpoints.

3. **Silent drop for filtered events.** Rejected work item types produce no PM tool comment (AC-014) to avoid notification noise on task or sub-task transitions. A structured log entry (AC-015) provides the audit trail for debugging without surfacing it to the end user.

4. **Exact case-sensitive string matching.** PM tools report issue types as exact strings (e.g. `"Story"` not `"story"`). Case-insensitive matching would risk accepting types the QA lead did not intend. The UI should present the types exactly as the PM tool names them; the field label in the component communicates this expectation.

5. **Max 20 types.** An upper bound prevents degenerate configurations and keeps the Cosmos document size predictable. Twenty types far exceeds any realistic PM tool configuration.

6. **No retroactive re-evaluation.** Events already queued on Service Bus when the filter is updated are not cancelled (AC-007). Implementing retroactive cancellation would require scanning the queue, which is expensive and error-prone. The constraint is communicated to the user via the UI confirmation copy.

## Layer Tags

| Tag | Scope |
|-----|-------|
| `[Domain]` | Entities, interfaces, value objects — `Testurio.Core` |
| `[Infra]` | Repositories, Cosmos config, Key Vault helpers, DI registration — `Testurio.Infrastructure` |
| `[App]` | DTOs, services, validators |
| `[API]` | Minimal API endpoints, route groups — `Testurio.Api` |
| `[Config]` | App configuration, constants, feature flags |
| `[UI]` | Types, API clients, hooks, MSW handlers, components, pages, i18n translation keys, route registration |
| `[Test]` | Unit, integration, and frontend component test files |
