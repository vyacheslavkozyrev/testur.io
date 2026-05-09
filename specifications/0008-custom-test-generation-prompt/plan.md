# Implementation Plan — Custom Test Generation Prompt (0008)

## Tasks

- [ ] T001 [Domain] Add `customPrompt` nullable string field to `Project` entity — `source/Testurio.Core/Entities/Project.cs`
- [ ] T002 [Infra] Update Cosmos DB project document mapping to include `customPrompt` — `source/Testurio.Infrastructure/Repositories/ProjectRepository.cs`
- [ ] T003 [App] Update `CreateProjectRequest` and `UpdateProjectRequest` DTOs with optional `customPrompt` field (max 5,000 chars) — `source/Testurio.Api/DTOs/ProjectDto.cs`
- [ ] T004 [App] Update `ProjectService.CreateAsync` and `ProjectService.UpdateAsync` to persist `customPrompt` — `source/Testurio.Api/Services/ProjectService.cs`
- [ ] T005 [App] Implement `PromptCheckService` — calls Claude API with prompt + strategy and returns structured feedback — `source/Testurio.Api/Services/PromptCheckService.cs`
- [ ] T006 [API] Add `POST /v1/projects/{projectId}/prompt-check` endpoint with ownership guard — `source/Testurio.Api/Endpoints/ProjectEndpoints.cs`
- [ ] T007 [Config] Define `PromptCheckFeedback` response model (Clarity, Specificity, PotentialConflicts) — `source/Testurio.Api/DTOs/PromptCheckDto.cs`
- [ ] T008 [Worker] Update `TestGeneratorPlugin` to compose final prompt (system + strategy + customPrompt) before calling Claude API — `source/Testurio.Plugins/TestGeneratorPlugin/TestGeneratorPlugin.cs`
- [ ] T009 [Worker] Add prompt length guard in `TestGeneratorPlugin` — fail the job with a descriptive log entry if total prompt exceeds context limit — `source/Testurio.Plugins/TestGeneratorPlugin/TestGeneratorPlugin.cs`
- [ ] T010 [UI] Add `customPrompt` to project types — `source/Testurio.Web/src/types/project.types.ts`
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

Domain and infrastructure are updated first to establish the data contract (`customPrompt` field) that all other layers depend on. The `PromptCheckService` and its endpoint are implemented before the UI hook and component so that the MSW mock can be validated against a real response shape. `TestGeneratorPlugin` is updated in parallel with the API layer since it reads from Cosmos and has no dependency on the portal API. Tests are always last, after all implementation layers are stable.

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
