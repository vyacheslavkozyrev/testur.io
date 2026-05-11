# Implementation Plan — PM Tool Integration (0007)

## Tasks

### T001 — [x] [Domain] Add PM tool configuration value objects to Project entity
Add enum types and configuration records to model ADO and Jira connections. Files: `source/Testurio.Core/Entities/Project.cs` (extend with `pmTool`, `adoOrgUrl`, `adoProjectName`, `adoTeam`, `adoInTestingStatus`, `adoAuthMethod`, `adoTokenSecretUri`, `jiraBaseUrl`, `jiraProjectKey`, `jiraInTestingStatus`, `jiraAuthMethod`, `jiraApiTokenSecretUri`, `jiraEmailSecretUri`, `webhookSecret`, `integrationStatus` properties), `source/Testurio.Core/Enums/PMToolType.cs` (`ado`, `jira`), `source/Testurio.Core/Enums/ADOAuthMethod.cs` (`pat`, `oauth`), `source/Testurio.Core/Enums/JiraAuthMethod.cs` (`apiToken`, `pat`), `source/Testurio.Core/Enums/IntegrationStatus.cs` (`none`, `active`, `authError`).

### T002 — [x] [Domain] Add PM tool API interfaces to Core
Define service contracts for interacting with PM tools. Files: `source/Testurio.Core/Interfaces/IADOClient.cs` (GetProjectAsync, TestConnectionAsync, PostCommentAsync, DeregisterWebhookAsync), `source/Testurio.Core/Interfaces/IJiraClient.cs` (GetProjectAsync, TestConnectionAsync, PostCommentAsync, DeregisterWebhookAsync).

### T003 — [x] [Infra] Create ADO REST client implementation
Implement Azure DevOps API client. Files: `source/Testurio.Infrastructure/ADO/ADOClient.cs` (full ADO REST v4.1 integration), `source/Testurio.Infrastructure/ADO/Models/ADOProjectResponse.cs`, `source/Testurio.Infrastructure/ADO/Models/ADOUserResponse.cs`.

### T004 — [x] [Infra] Create Jira REST client implementation
Implement Jira API client. Files: `source/Testurio.Infrastructure/Jira/JiraAdditionalClient.cs` (extends existing JiraApiClient from 0004 with GetProjectAsync, TestConnectionAsync, DeregisterWebhookAsync), `source/Testurio.Infrastructure/Jira/Models/JiraProjectResponse.cs`.

### T005 — [x] [Infra] Add webhook secret generation and storage
Create utility for generating and persisting webhook secrets. Files: `source/Testurio.Infrastructure/Security/WebhookSecretGenerator.cs` (GenerateSecret(), ValidateSignature()), register in DI.

### T006 — [x] [Infra] Extend ProjectRepository for PM tool queries
Add specialized queries for retrieving PM tool config. Files: `source/Testurio.Infrastructure/Cosmos/ProjectRepository.cs` (add GetByProjectIdAndUserIdAsync if not present, ensure partition-key enforcement).

### T007 — [x] [App] Create PM tool connection validator
Validate ADO and Jira connection form inputs. Files: `source/Testurio.Api/Validators/ADOConnectionValidator.cs`, `source/Testurio.Api/Validators/JiraConnectionValidator.cs`, `source/Testurio.Api/Validators/PMToolConnectionValidator.cs` (base class).

### T008 — [x] [App] Create PM tool connection service
Orchestrate PM tool configuration save, test, and removal. Files: `source/Testurio.Api/Services/IPMToolConnectionService.cs`, `source/Testurio.Api/Services/PMToolConnectionService.cs` (SaveADOConnectionAsync, SaveJiraConnectionAsync, TestConnectionAsync, RemoveConnectionAsync, GenerateWebhookSecretAsync, RegenerateWebhookSecretAsync).

### T009 — [x] [App] Create DTOs for PM tool configuration
Define request and response models. Files: `source/Testurio.Api/DTOs/SaveADOConnectionRequest.cs`, `source/Testurio.Api/DTOs/SaveJiraConnectionRequest.cs`, `source/Testurio.Api/DTOs/PMToolConnectionResponse.cs`, `source/Testurio.Api/DTOs/TestConnectionResponse.cs`, `source/Testurio.Api/DTOs/WebhookSetupResponse.cs`.

### T010 — [x] [API] Add PM tool connection endpoints
Create routes for configuration, testing, and removal. Files: `source/Testurio.Api/Program.cs` (add route group `/v1/projects/{projectId}/integrations` with POST/PUT/DELETE/GET and POST test), routing logic maps to `PMToolConnectionService`.

### T011 — [x] [API] Add webhook info endpoint
Create route to retrieve webhook URL and secret display state. Files: `source/Testurio.Api/Program.cs` (extend integrations group with GET `/webhook-setup` returning `WebhookSetupResponse`).

### T012 — [x] [API] Add integration status endpoint
Create route to retrieve current PM tool integration state. Files: `source/Testurio.Api/Program.cs` (GET `/v1/projects/{projectId}/integrations` returning integration status, tool name, identifiers, auth method).

### T013 — [x] [Config] Register PM tool clients and services in DI
Register all infrastructure and application services. Files: `source/Testurio.Api/Program.cs` and/or `source/Testurio.Infrastructure/DependencyInjection.cs` (AddScoped<IADOClient, ADOClient>(), AddScoped<IJiraClient, JiraAdditionalClient>(), AddScoped<IPMToolConnectionService, PMToolConnectionService>(), AddSingleton<WebhookSecretGenerator>()).

### T014 — [UI] Create PM tool types and DTOs
Define TypeScript interfaces matching backend models. Files: `source/Testurio.Web/src/types/pmTool.types.ts` (PMToolType, ADOConnection, JiraConnection, TestConnectionResult, WebhookSetupInfo, IntegrationStatus).

### T015 — [UI] Create PM tool API service
Implement client-side API calls. Files: `source/Testurio.Web/src/services/pmTool/pmToolService.ts` (saveADOConnection, saveJiraConnection, testConnection, removeConnection, getWebhookSetup, getIntegrationStatus), all methods use apiClient with proper error handling.

### T016 — [UI] Create MSW mock handlers for PM tool endpoints
Mock backend responses for development and testing. Files: `source/Testurio.Web/src/mocks/handlers/pmTool.ts` (handlers for all PM tool endpoints with realistic responses and error scenarios).

### T017 — [UI] Create React Query hooks for PM tool management
Implement query and mutation hooks. Files: `source/Testurio.Web/src/hooks/usePMToolConnection.ts` (useIntegrationStatus, useSaveADOConnection, useSaveJiraConnection, useTestConnection, useRemoveConnection, useWebhookSetup, useRegenerateWebhookSecret, PM_TOOL_KEYS), follow React Query v5 patterns with proper cache invalidation.

### T018 — [UI] Create ADO connection form component
Build form for Azure DevOps configuration. Files: `source/Testurio.Web/src/components/Integrations/ADOConnectionForm/ADOConnectionForm.tsx` (required fields: org URL, project name, team, In Testing status, auth method selector, validation, form state management).

### T019 — [UI] Create Jira connection form component
Build form for Jira configuration. Files: `source/Testurio.Web/src/components/Integrations/JiraConnectionForm/JiraConnectionForm.tsx` (required fields: base URL, project key, In Testing status, auth method selector with email+token or PAT options, validation).

### T020 — [UI] Create test connection button component
Build reusable button with result indicator. Files: `source/Testurio.Web/src/components/Integrations/TestConnectionButton/TestConnectionButton.tsx` (button, success/auth-error/unreachable indicators, loading state, error messages).

### T021 — [UI] Create webhook setup display component
Build panel showing webhook URL and secret. Files: `source/Testurio.Web/src/components/Integrations/WebhookSetupPanel/WebhookSetupPanel.tsx` (URL display with copy button, secret masked/plaintext toggle, regenerate button with warning, tool-specific instructions, copy-to-clipboard functionality).

### T022 — [UI] Create integration status card component
Build summary card showing current integration state. Files: `source/Testurio.Web/src/components/Integrations/IntegrationStatusCard/IntegrationStatusCard.tsx` (not configured state, configured state with tool name/identifier, auth error alert with token update form).

### T023 — [UI] Create integration removal dialog
Build confirmation and removal flow. Files: `source/Testurio.Web/src/components/Integrations/RemoveIntegrationDialog/RemoveIntegrationDialog.tsx` (confirmation warning about active runs and queued jobs, removal execution, success feedback).

### T024 — [UI] Create Integrations settings page
Assemble all components into unified settings view. Files: `source/Testurio.Web/src/pages/ProjectSettings/Integrations/IntegrationPage.tsx` (renders status card, connection forms, test button, webhook setup, removal dialog as appropriate to current state).

### T025 — [UI] Add translations for PM tool integration
Localize all user-facing text. Files: `source/Testurio.Web/src/locales/en/pmTool.json` (connection labels, field names, instructions, error messages, success messages, auth method options, tool-specific steps).

### T026 — [UI] Register integration routes in main router
Wire page into navigation. Files: `source/Testurio.Web/src/routes/routes.tsx` (add route for project integrations page under authenticated project settings area).

### T027 — [Test] Create PM tool connection service unit tests
Test validation, secret management, and service logic. Files: `tests/Testurio.UnitTests/Services/PMToolConnectionServiceTests.cs` (SaveADOConnectionAsync, SaveJiraConnectionAsync, TestConnectionAsync with success/auth-error/unreachable, RemoveConnectionAsync, WebhookSecretAsync, all US stories' ACs).

### T028 — [Test] Create PM tool API integration tests
Test endpoint behavior and multi-tenancy. Files: `tests/Testurio.IntegrationTests/Controllers/PMToolIntegrationTests.cs` (POST/PUT/DELETE/GET integrations, webhook setup, test connection, cross-tenant forbidden checks).

### T029 — [Test] Create PM tool React Query hook tests
Test query behavior and cache invalidation. Files: `source/Testurio.Web/src/hooks/__tests__/usePMToolConnection.test.ts` (query invalidation on mutation, error state handling, loading states).

### T030 — [Test] Create integration form component tests
Test form validation and submission. Files: `source/Testurio.Web/src/components/Integrations/ADOConnectionForm/ADOConnectionForm.test.tsx`, `source/Testurio.Web/src/components/Integrations/JiraConnectionForm/JiraConnectionForm.test.tsx` (field presence, validation display, submission).

### T031 — [Test] Create webhook setup component tests
Test secret masking, copy, regenerate flows. Files: `source/Testurio.Web/src/components/Integrations/WebhookSetupPanel/WebhookSetupPanel.test.tsx` (URL and secret rendering, copy-to-clipboard, regenerate confirmation, mask/plaintext toggle).

## Rationale

### Ordering Principle

Tasks follow the classic dependency chain: Domain (entities, enums, contracts) → Infrastructure (implementations of those contracts, storage) → Application (business logic, validation, DTOs) → API (HTTP routing and middleware) → Configuration (DI setup) → UI (components, services, hooks) → Tests (covering all layers).

This ordering ensures:

1. **Domain first (T001–T002):** Establishes the PM tool configuration model and service contracts so all subsequent layers have clear types.
2. **Infrastructure second (T003–T006):** Implements the external service clients (ADO, Jira) and secret storage patterns that the application layer will depend on.
3. **Application third (T007–T009):** Builds the core business logic (validators, connection service, DTOs) that transforms user input into domain state and orchestrates infrastructure calls.
4. **API fourth (T010–T012):** Exposes the application services as HTTP endpoints. The routing and error handling depend on the validators and service logic already being in place.
5. **Configuration fifth (T013):** Registers all the above in DI. This is separate from implementation to keep it visible and because registration depends on all implementations being compiled.
6. **UI sixth (T014–T026):** Builds the frontend. Types are derived from domain; services call the API endpoints; components consume the React Query hooks; routing wires it all together.
7. **Tests last (T027–T031):** Unit tests for services, integration tests for API, and component/hook tests for frontend—each level tests the layer below it.

### Cross-Feature Dependencies

**Feature 0006 (Project Creation & Core Configuration)** must be implemented first. Feature 0007 extends the Project entity with PM tool fields and requires project CRUD to work. The Project entity's base structure (projectId, userId, timestamps, soft delete) comes from 0006.

**Feature 0001 (Automatic Test Run Trigger)** and **Feature 0004 (Test Report Delivery)** are already complete. Feature 0007 does NOT implement webhook handling (0001 does that) or report posting (0004 does that). Feature 0007 only handles PM tool *configuration* and *credential storage*. The ReportWriter plugin (0004) will use the stored PM tool config when posting results; the webhook receivers (0001) ignore PM tool config—they listen for events and enqueue jobs. This feature bridges the two by storing the connection details that those features will use.

**No blocking forward dependencies:** Features 0019 (Polling), 0020 (Work item type filtering), 0024 (Status transitions), 0009 (Report format) all depend on PM tool config being available. Those features can only start once 0007 is complete. This plan does not include their scope.

### Architectural Decisions

1. **Key Vault Secret References:** All sensitive credentials (ADO PAT, Jira API token, Jira email, webhook secret) are stored in Azure Key Vault. Only a Key Vault secret URI is persisted in the Cosmos project document. This prevents secrets from ever touching the database and aligns with the architecture's multi-tenancy model: a user's credentials are isolated per project in a Key Vault namespace.

2. **Webhook Secret Generation:** The webhook secret is generated at configuration save time (T005), stored in Key Vault, and displayed once to the user in plaintext. On subsequent views, it is masked. Users can regenerate it, which invalidates the old secret immediately and requires webhook re-registration in the PM tool. This provides a password-reset pattern without requiring a second authentication flow.

3. **Test Connection Endpoint:** The test connection (US-003) is a dedicated endpoint that uses the stored credentials to make a lightweight call to the PM tool (fetch project metadata or current user). This verifies the token is valid before the user registers a webhook, catching auth errors early.

4. **Removal Cleanup:** When a PM tool integration is removed (US-007), the backend must stop any active test run and remove queued jobs for that project (AC-046). This requires coordination with the test pipeline. The removal service will call into the run management layer to cancel active runs and dequeue messages before deleting the PM tool config.

5. **Token Expiry Handling:** When the Report Writer plugin (0004) or Test Connection (0007) receives a 401 from the PM tool, the project's `integrationStatus` is set to `"auth_error"`. The portal displays a banner with an inline form to update the token (US-006). Updating the token doesn't require re-entering the URL or other fields—only the credential is replaced in Key Vault.

6. **Per-Project Connection Only:** Feature scope is "one PM tool connection per project" (AC-013 of 0007 out-of-scope). Supporting multiple PM tool connections per project (e.g., post results to both ADO and Jira) is explicitly out of scope for v1.

### Implementation Sequence Within Each Layer

- **Domain:** Enums are defined first (so they can be used in entity properties), then the Project entity is extended.
- **Infra:** API clients are implemented before the repository, so the repository can use them if needed (though T006 is mostly about ensuring partition-key enforcement).
- **App:** Validators before the service (the service uses them), DTOs alongside (independent).
- **API:** Endpoints last, after DTOs and services exist. DI registration happens after all code is written.
- **UI:** Types before services (services have typed return types), services before hooks (hooks call services), hooks before components (components use hooks), components before pages (pages compose components), translations and routing last (they depend on everything).
- **Tests:** Follow the same layer order, testing each layer from bottom to top.

This ensures no forward references, no missing dependencies, and a clear verification path (each task's deliverable can be validated before the next begins).

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
