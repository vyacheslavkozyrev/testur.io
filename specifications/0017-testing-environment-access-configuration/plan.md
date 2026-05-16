# Implementation Plan — Testing Environment Access Configuration (0017)

## Tasks

- [ ] T001 [Domain] Add `AccessMode` enum (`IpAllowlist`, `BasicAuth`, `HeaderToken`) — `source/Testurio.Core/Enums/AccessMode.cs`
- [ ] T002 [Domain] Extend `Project` entity with access configuration fields (`AccessMode`, `BasicAuthUserSecretUri`, `BasicAuthPassSecretUri`, `HeaderTokenName`, `HeaderTokenSecretUri`) — `source/Testurio.Core/Entities/Project.cs`
- [ ] T003 [Domain] Define `IProjectAccessCredentialProvider` interface with `ResolveAsync(Project, CancellationToken)` returning `ProjectAccessCredentials` — `source/Testurio.Core/Interfaces/IProjectAccessCredentialProvider.cs`
- [ ] T004 [Domain] Create `ProjectAccessCredentials` discriminated union record (`IpAllowlist`, `BasicAuth`, `HeaderToken` cases) — `source/Testurio.Core/Models/ProjectAccessCredentials.cs`
- [ ] T005 [Domain] Create `CredentialRetrievalException` — `source/Testurio.Core/Exceptions/CredentialRetrievalException.cs`
- [ ] T006 [Infra] Implement `ProjectAccessCredentialProvider` — reads secret URIs from project document, fetches values from Key Vault via `ISecretResolver`, returns typed `ProjectAccessCredentials` — `source/Testurio.Infrastructure/KeyVault/ProjectAccessCredentialProvider.cs`
- [ ] T007 [Infra] Extend `ProjectSecretNamespace` with constants for access secret keys (`basic-auth-user`, `basic-auth-pass`, `header-token-value`) — `source/Testurio.Infrastructure/KeyVault/ProjectSecretNamespace.cs`
- [ ] T008 [Infra] Register `IProjectAccessCredentialProvider` → `ProjectAccessCredentialProvider` in DI — `source/Testurio.Infrastructure/DependencyInjection.cs`
- [ ] T009 [App] Create `UpdateProjectAccessRequest` DTO with `[AllowedValues]` on `AccessMode`, conditional required fields, and `[RegularExpression]` on `HeaderTokenName` — `source/Testurio.Api/DTOs/ProjectAccessDto.cs`
- [ ] T010 [App] Create `ProjectAccessDto` (safe response: mode, optional username, optional header name — never plaintext secrets) — `source/Testurio.Api/DTOs/ProjectAccessDto.cs`
- [ ] T011 [App] Implement `IProjectAccessService` interface and `ProjectAccessService` — validates ownership, writes/clears Key Vault secrets, updates Cosmos project document, returns `ProjectAccessDto` — `source/Testurio.Api/Services/ProjectAccessService.cs`
- [ ] T012 [API] Register `PATCH /v1/projects/{projectId}/access` and `GET /v1/projects/{projectId}/access` endpoints — `source/Testurio.Api/Endpoints/ProjectAccessEndpoints.cs`
- [ ] T013 [App] Extend `HttpExecutor` to inject Basic Auth header or custom header per run using `IProjectAccessCredentialProvider` — `source/Testurio.Pipeline.Executors/HttpExecutor.cs`
- [ ] T014 [App] Extend `PlaywrightExecutor` to apply `httpCredentials` or `extraHTTPHeaders` per run using `IProjectAccessCredentialProvider` — `source/Testurio.Pipeline.Executors/PlaywrightExecutor.cs`
- [ ] T015 [UI] Add access configuration types — `source/Testurio.Web/src/types/projectAccess.types.ts`
- [ ] T016 [UI] Add API client for access endpoints — `source/Testurio.Web/src/services/project/projectAccessService.ts`
- [ ] T017 [UI] Add React Query hooks (`useProjectAccess`, `useUpdateProjectAccess`) — `source/Testurio.Web/src/hooks/useProjectAccess.ts`
- [ ] T018 [UI] Add MSW mock handlers for access endpoints — `source/Testurio.Web/src/mocks/handlers/projectAccess.ts`
- [ ] T019 [UI] Create `AccessModeSelector` component (radio group + conditional credential fields + IP info panel) — `source/Testurio.Web/src/components/AccessModeSelector/AccessModeSelector.tsx`
- [ ] T020 [UI] Integrate `AccessModeSelector` into `ProjectSettingsPage` — `source/Testurio.Web/src/pages/ProjectSettingsPage/ProjectSettingsPage.tsx`
- [ ] T021 [UI] Add translation keys — `source/Testurio.Web/src/locales/en/projectAccess.json`
- [ ] T022 [Test] Backend unit tests for `ProjectAccessService` (ownership, secret write/clear on mode switch, safe DTO shape) — `tests/Testurio.UnitTests/Services/ProjectAccessServiceTests.cs`
- [ ] T023 [Test] Backend unit tests for `ProjectAccessCredentialProvider` (each access mode, Key Vault unreachable → `CredentialRetrievalException`) — `tests/Testurio.UnitTests/Services/ProjectAccessCredentialProviderTests.cs`
- [ ] T024 [Test] Backend integration tests for `PATCH /v1/projects/{projectId}/access` and `GET /v1/projects/{projectId}/access` — `tests/Testurio.IntegrationTests/Controllers/ProjectAccessControllerTests.cs`
- [ ] T025 [Test] Frontend component tests for `AccessModeSelector` (mode switch renders correct fields, masked password field, validation errors) — `source/Testurio.Web/src/components/AccessModeSelector/AccessModeSelector.test.tsx`

## Rationale

**Domain first, then infrastructure, then API, then UI, then tests** — this is the standard layer order mandated by the project rules.

T001–T005 establish the domain contracts that all downstream layers depend on. The `AccessMode` enum and `Project` entity extension (T001–T002) define the data model. `IProjectAccessCredentialProvider` and `ProjectAccessCredentials` (T003–T004) define the runtime interface consumed by both the API service layer and the pipeline executors — neither can be implemented without these contracts. `CredentialRetrievalException` (T005) is a domain-level exception that both infrastructure and pipeline stages reference.

T006–T008 are the infrastructure layer. `ProjectAccessCredentialProvider` (T006) is the concrete credential resolver, built on the existing `ISecretResolver` abstraction already in place. T007 extends the existing `ProjectSecretNamespace` rather than creating a new file, keeping secret naming centrally defined. DI registration (T008) wires the new interface before any consumer is registered.

T009–T012 are the API application and endpoint layer. The DTOs (T009–T010) are defined before the service (T011) so the service can reference them directly. The service is implemented before the endpoint (T012) because the endpoint delegates entirely to the service. The `PATCH` endpoint uses the existing `ValidationFilter<T>` pattern for consistent validation.

T013–T014 extend the existing `HttpExecutor` and `PlaywrightExecutor` to inject credentials. These tasks are placed after the domain and infrastructure layers because the executors depend on `IProjectAccessCredentialProvider` and `ProjectAccessCredentials`. They are ordered before UI (T015+) because they are backend pipeline tasks; the UI section covers the portal settings experience independently.

T015–T021 follow the canonical UI layer order from `ui.md`: types → service → hook → mock → component → page → i18n. No new route registration is required because the access configuration section is integrated into the existing `ProjectSettingsPage`.

T022–T025 are the test layer, always last per the QA rules. Unit tests are ordered before integration tests; backend tests before frontend tests.

**Cross-feature dependencies:**

- **Depends on 0006 (Project Creation & Core Configuration):** The `Project` entity, `IProjectRepository`, `ProjectRepository`, `ProjectService`, and `ProjectEndpoints` are all fully implemented. This feature extends the entity and adds a parallel service/endpoint for access configuration — no changes to 0006's code are required beyond the entity field additions in T002.
- **Blocks 0023 (Multiple Authentication Methods for API Test Execution):** 0023 adds Bearer token, API key, and Basic Auth for API *test request* auth (the requests sent by the executor against the product API). Feature 0017 covers *environment access* auth (how the executor reaches the staging environment). The credential provider interface established here may be referenced by 0023's implementation.
- **Blocks 0029 (Executor Router):** 0029's out-of-scope section explicitly defers environment access credential injection to this feature. T013–T014 deliver the executor-level credential application that 0029 left incomplete.
- **No dependency on 0007 (PM Tool Integration):** Access configuration is independent of which PM tool a project uses.

No EF Core migration step is required because Testurio uses Azure Cosmos DB — the new fields are added to the `Project` document schema without a formal migration.

## Layer Tags

| Tag | Scope |
|-----|-------|
| `[Domain]` | Entities, interfaces, value objects, enums — `Testurio.Core` |
| `[Infra]` | Key Vault provider, DI registration — `Testurio.Infrastructure` |
| `[App]` | DTOs, services, executor extensions |
| `[API]` | Minimal API endpoints, route groups — `Testurio.Api` |
| `[UI]` | Types, API clients, hooks, MSW handlers, components, pages, i18n translation keys |
| `[Test]` | Unit, integration, and frontend component test files |
