# Implementation Plan — Project Creation & Core Configuration (0006)

## Tasks

- [x] T001 [Domain] Create `Project` entity — `source/Testurio.Core/Entities/Project.cs`
- [x] T002 [Domain] Add `IProjectRepository` interface — `source/Testurio.Core/Interfaces/IProjectRepository.cs`
- [x] T003 [Infra] Implement `ProjectRepository` (Cosmos DB) — `source/Testurio.Infrastructure/Repositories/ProjectRepository.cs`
- [x] T004 [Infra] Register repository in DI — `source/Testurio.Infrastructure/DependencyInjection.cs`
- [x] T005 [Infra] Add Key Vault namespace provisioning helper — `source/Testurio.Infrastructure/KeyVault/ProjectSecretNamespace.cs`
- [ ] T006 [App] Create `ProjectDto` and `CreateProjectRequest` / `UpdateProjectRequest` — `source/Testurio.Api/DTOs/ProjectDto.cs`
- [ ] T007 [App] Implement `ProjectService` (CRUD + soft delete, Key Vault namespace init) — `source/Testurio.Api/Services/ProjectService.cs`
- [ ] T008 [API] Register project endpoints — `source/Testurio.Api/Endpoints/ProjectEndpoints.cs`
- [ ] T009 [UI] Add API types — `source/Testurio.Web/src/types/project.types.ts`
- [ ] T010 [UI] Add API client — `source/Testurio.Web/src/services/project/projectService.ts`
- [ ] T011 [UI] Add React Query hook — `source/Testurio.Web/src/hooks/useProject.ts`
- [ ] T012 [UI] Add MSW mock handler — `source/Testurio.Web/src/mocks/handlers/project.ts`
- [ ] T013 [UI] Create project form component (shared create/edit) — `source/Testurio.Web/src/components/ProjectForm/ProjectForm.tsx`
- [ ] T014 [UI] Create project delete confirmation dialog — `source/Testurio.Web/src/components/ProjectDeleteDialog/ProjectDeleteDialog.tsx`
- [ ] T015 [UI] Add project settings page — `source/Testurio.Web/src/pages/ProjectSettingsPage/ProjectSettingsPage.tsx`
- [ ] T016 [UI] Add translation keys — `source/Testurio.Web/src/locales/en/project.json`
- [ ] T017 [UI] Register routes — `source/Testurio.Web/src/routes/routes.tsx`
- [ ] T018 [Test] Backend unit tests — `tests/Testurio.UnitTests/Services/ProjectServiceTests.cs`
- [ ] T019 [Test] Backend integration tests — `tests/Testurio.IntegrationTests/Controllers/ProjectControllerTests.cs`
- [ ] T020 [Test] Frontend component tests — `source/Testurio.Web/src/components/ProjectForm/ProjectForm.test.tsx`
- [ ] T021 [Test] E2E tests — `source/Testurio.Web/e2e/project-creation.spec.ts`

## Rationale

Domain entities and interfaces are defined first so all downstream layers depend on stable contracts. The Cosmos repository and DI registration follow, providing the data access layer the API service needs. The Key Vault namespace helper is wired at infrastructure level so the service layer can call it without knowing Azure SDK details. DTOs and the service layer come next; they encapsulate validation and business rules independently of transport. The Minimal API endpoint layer registers routes last, after all dependencies are resolvable. UI work proceeds in the canonical order (types → service → hook → mock → component → page → i18n → route) so each layer compiles against its dependency. Tests are always last.

No EF Core migration step exists here because Testurio uses Azure Cosmos DB directly — there is no relational schema to migrate.

## Layer Tags

| Tag | Scope |
|-----|-------|
| `[Domain]` | Entities, interfaces, value objects — `Testurio.Core` |
| `[Infra]` | Repositories, Cosmos config, Key Vault helpers, DI registration — `Testurio.Infrastructure` |
| `[App]` | DTOs, services, validators |
| `[API]` | Minimal API endpoints, route groups — `Testurio.Api` |
| `[UI]` | Types, API clients, hooks, MSW handlers, components, pages, i18n translation keys, route registration |
| `[Test]` | Unit, integration, and frontend component test files |
