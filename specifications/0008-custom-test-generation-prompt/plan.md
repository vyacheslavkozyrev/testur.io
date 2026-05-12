# Implementation Plan — Custom Test Generation Prompt (0008)

## Tasks

- [x] T001 [Domain] Add `customPrompt` nullable string field to `Project` entity — `source/Testurio.Core/Entities/Project.cs`
- [x] T002 [Infra] Update Cosmos DB project document mapping to include `customPrompt` — `source/Testurio.Infrastructure/Repositories/ProjectRepository.cs`
- [x] T003 [App] Update `CreateProjectRequest` and `UpdateProjectRequest` DTOs with optional `customPrompt` field (max 5,000 chars) — `source/Testurio.Api/DTOs/ProjectDto.cs`
- [x] T004 [App] Update `ProjectService.CreateAsync` and `ProjectService.UpdateAsync` to persist `customPrompt` — `source/Testurio.Api/Services/ProjectService.cs`
- [x] T005 [App] Implement `PromptCheckService` — calls Claude API with prompt + strategy and returns structured feedback — `source/Testurio.Api/Services/PromptCheckService.cs`
- [x] T006 [API] Add `POST /v1/projects/{projectId}/prompt-check` endpoint with ownership guard — `source/Testurio.Api/Endpoints/ProjectEndpoints.cs`
- [x] T007 [Config] Define `PromptCheckFeedback` response model (Clarity, Specificity, PotentialConflicts) — `source/Testurio.Api/DTOs/PromptCheckDto.cs`
- [x] T008 [Worker] Update `TestGeneratorPlugin` to compose final prompt (system + strategy + customPrompt) before calling Claude API — `source/Testurio.Plugins/TestGeneratorPlugin/TestGeneratorPlugin.cs`
- [x] T009 [Worker] Add prompt length guard in `TestGeneratorPlugin` — fail the job with a descriptive log entry if total prompt exceeds context limit — `source/Testurio.Plugins/TestGeneratorPlugin/TestGeneratorPlugin.cs`
- [x] T010 [UI] Add `customPrompt` to project types — `source/Testurio.Web/src/types/project.types.ts`
- [ ] T011 [UI] Add `promptCheck` method to project service — `source/Testurio.Web/src/services/project/projectService.ts`
- [ ] T012 [UI] Add `usePromptCheck` mutation hook — `source/Testurio.Web/src/hooks/useProject.ts`
- [ ] T013 [UI] Add MSW mock handlers for `PATCH /v1/projects/:id` (customPrompt) and `POST /v1/projects/:id/prompt-check` — `source/Testurio.Web/src/mocks/handlers/project.ts`
- [ ] T014 [UI] Build `CustomPromptField` component — textarea with live character counter, conflict warning, prompt preview panel, and "Check Prompt" button with inline feedback — `source/Testurio.Web/src/components/CustomPromptField/CustomPromptField.tsx`
- [ ] T015 [UI] Integrate `CustomPromptField` into the project settings page — `source/Testurio.Web/src/pages/ProjectSettingsPage/ProjectSettingsPage.tsx`
- [ ] T016 [UI] Add translation keys — `source/Testurio.Web/src/locales/en/project.json`
- [ ] T017 [Test] Backend unit tests for `PromptCheckService` and prompt composition logic in `TestGeneratorPlugin` — `tests/Testurio.UnitTests/Services/PromptCheckServiceTests.cs`
- [ ] T018 [Test] Backend integration tests for `POST /v1/projects/{projectId}/prompt-check` endpoint — `tests/Testurio.IntegrationTests/Controllers/ProjectPromptCheckControllerTests.cs`
- [ ] T019 [Test] Frontend component tests for `CustomPromptField` — `source/Testurio.Web/src/components/CustomPromptField/CustomPromptField.test.tsx`
- [ ] T020 [Test] E2E tests — `source/Testurio.Web/e2e/custom-test-generation-prompt.spec.ts`

## Rationale

### Dependency Analysis

Feature 0008 depends on feature 0006 (Project Creation & Core Configuration) being complete, as it adds a new field to the Project entity. Feature 0001 (Automatic Test Run Trigger) and 0004 (Test Report Delivery) are already complete and provide the foundational testing pipeline that TestGeneratorPlugin integrates with.

### Ordering Rationale

**Domain Layer (T001):** The `customPrompt` field must be added to the Project entity first, before any other layer can reference it. This establishes the data contract.

**Infrastructure Layer (T002):** Cosmos DB mapping is updated immediately after the domain entity, ensuring the field is persisted correctly. This is foundational for all downstream layers.

**Application Layer (T003-T005):** DTOs and services are implemented next. The `PromptCheckService` is built before the API endpoint (T006) so the endpoint logic remains minimal and testable. The service encapsulates the Claude API call and feedback structure.

**Configuration Layer (T007):** The `PromptCheckFeedback` response model is defined alongside application services, making it available for both backend and frontend usage.

**API Layer (T006):** The `POST /v1/projects/{projectId}/prompt-check` endpoint is implemented once the service is ready. Ownership validation is performed using extracted `userId` from the JWT.

**Worker Layer (T008-T009):** `TestGeneratorPlugin` is updated in parallel with the API implementation since it reads from Cosmos (no dependency on the portal API). The prompt composition logic and length validation must be implemented before tests can run correctly.

**UI Layer (T010-T016):** Frontend types, services, hooks, and components are implemented after all backend infrastructure is in place. MSW handlers (T013) mock both the PATCH endpoint for saving `customPrompt` and the new POST prompt-check endpoint. The `CustomPromptField` component (T014) integrates live character counting, conflict detection, prompt preview, and the "Check Prompt" button. Integration into ProjectSettingsPage (T015) happens after the component is complete. Translation keys (T016) are added last in the UI sequence.

**Test Layer (T017-T020):** Tests span backend unit tests (PromptCheckService, prompt composition), integration tests (the prompt-check endpoint), and frontend tests (CustomPromptField component, E2E). Tests are always last, after all implementation layers are stable.

### Key Design Constraints

- **No prompt library or saved templates:** Each project has exactly one custom prompt; saving a new value overwrites the previous one.
- **Additive prompt composition:** The custom prompt never overrides the testing strategy; it is always appended at the end: system prompt → testing strategy → custom prompt.
- **Client-side and server-side validation:** The UI enforces a 5,000 character limit with a live counter; the API validates independently.
- **Multi-tenancy enforcement:** The `POST /api/projects/{projectId}/prompt-check` endpoint requires ownership validation (extracted `userId` must match the project's `userId`); no cross-tenant access is possible.
- **Context length guard:** The worker adds a length check to prevent the final prompt from exceeding Claude API context limits; jobs fail gracefully with descriptive errors if the limit is exceeded.

## Layer Tags

| Tag | Scope |
|-----|-------|
| `[Domain]` | Entities, interfaces, value objects — `Testurio.Core` |
| `[Infra]` | Repositories, Cosmos mapping, DI registration — `Testurio.Infrastructure` |
| `[App]` | DTOs, services, validators |
| `[API]` | Minimal API endpoints, route groups — `Testurio.Api` |
| `[Config]` | Response models, constants |
| `[Worker]` | Pipeline stage logic — `Testurio.Plugins` |
| `[UI]` | Types, API clients, hooks, MSW handlers, components, pages, i18n translation keys |
| `[Test]` | Unit, integration, and frontend component test files |
