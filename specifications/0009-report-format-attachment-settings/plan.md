# Implementation Plan — Report Format & Attachment Settings (0009)

## Tasks

T001. ~~`[Migration]` Add report template and attachment toggle columns to Project Cosmos schema~~
→ `source/Testurio.Core/Entities/Project.cs` ✅

T002. ~~`[Domain]` Create domain entities and interfaces for report templating~~
→ `source/Testurio.Core/Interfaces/IBlobStorageClient.cs`, `source/Testurio.Core/Interfaces/IReportTemplateService.cs` ✅

T003. ~~`[Infra]` Implement Azure Blob Storage client~~
→ `source/Testurio.Infrastructure/BlobStorage/BlobStorageClient.cs` ✅

T004. ~~`[Infra]` Register blob storage client in DI container~~
→ `source/Testurio.Infrastructure/DependencyInjection.cs` ✅

T005. ~~`[Infra]` Implement report template upload/download repository~~
→ `source/Testurio.Infrastructure/BlobStorage/TemplateRepository.cs` ✅

T006. ~~`[App]` Create DTOs for report template upload requests and responses~~
→ `source/Testurio.Api/Dtos/ReportTemplateUploadRequest.cs`, `source/Testurio.Api/Dtos/ReportTemplateUploadResponse.cs` ✅

T007. ~~`[App]` Implement report template service with validation and token scanning~~
→ `source/Testurio.Api/Services/ReportTemplateService.cs` ✅

T008. ~~`[API]` Add project settings endpoints for template upload, replacement, and removal~~
→ `source/Testurio.Api/Controllers/ProjectSettingsController.cs` (new route group `/v1/projects/{id}/report-settings`) ✅

T009. ~~`[Infra]` Extend ReportBuilderService to support custom template rendering with placeholder token substitution~~
→ `source/Testurio.Plugins/ReportWriterPlugin/ReportBuilderService.cs` (new overload method) ✅

T010. ~~`[Infra]` Create TemplateRenderer service for placeholder token substitution and safe fallback handling~~
→ `source/Testurio.Plugins/ReportWriterPlugin/TemplateRenderer.cs` ✅

T011. ~~`[Infra]` Create built-in default report template constant~~
→ `source/Testurio.Plugins/ReportWriterPlugin/DefaultReportTemplate.cs` ✅

T012. ~~`[Infra]` Extend ReportWriterPlugin to load and render custom templates, storing report blob URI~~
→ `source/Testurio.Plugins/ReportWriterPlugin/ReportWriterPlugin.cs` (modify DeliverAsync method) ✅

T013. ~~`[App]` Implement validators for project report configuration (test_type vs reportIncludeScreenshots rules)~~
→ `source/Testurio.Api/Validators/ReportConfigurationValidator.cs` ✅

T014. ~~`[API]` Add GET endpoint to retrieve current project report settings~~
→ `source/Testurio.Api/Controllers/ProjectSettingsController.cs` ✅

T015. ~~`[API]` Add PATCH endpoint to update report attachment toggles~~
→ `source/Testurio.Api/Controllers/ProjectSettingsController.cs` ✅

T016. ~~`[UI]` Create TypeScript types for report template and attachment settings~~
→ `source/portal/src/types/reportSettings.types.ts` ✅

T017. ~~`[UI]` Implement blob storage upload service for frontend~~
→ `source/portal/src/services/reportTemplate/reportTemplateService.ts` ✅

T018. ~~`[UI]` Create React hooks for report settings queries and mutations~~
→ `source/portal/src/hooks/useReportSettings.ts` ✅

T019. ~~`[UI]` Create MSW mock handlers for report settings endpoints~~
→ `source/portal/src/mocks/handlers/reportSettings.ts` ✅

T020. ~~`[UI]` Create ReportTemplateUpload component with file validation and token warning display~~
→ `source/portal/src/components/ReportTemplateUpload/ReportTemplateUpload.tsx` ✅

T021. ~~`[UI]` Create ReportAttachmentToggles component with test_type-aware screenshot toggle~~
→ `source/portal/src/components/ReportAttachmentToggles/ReportAttachmentToggles.tsx` ✅

T022. ~~`[UI]` Create ProjectReportSettingsSection page integrating both components~~
→ `source/portal/src/pages/ProjectSettings/ReportSettingsSection.tsx` ✅

T023. ~~`[UI]` Add report settings i18n translation keys~~
→ `source/portal/src/locales/en/reportSettings.json` ✅

T024. ~~`[UI]` Register report settings routes in project settings navigation~~
→ `source/portal/src/routes/routes.tsx` ✅

T025. ~~`[Test]` Unit tests for ReportTemplateService validation and token scanning~~
→ `tests/Testurio.UnitTests/Services/ReportTemplateServiceTests.cs` ✅

T026. ~~`[Test]` Unit tests for TemplateRenderer placeholder substitution logic~~
→ `tests/Testurio.UnitTests/Services/TemplateRendererTests.cs` ✅

T027. ~~`[Test]` Unit tests for report configuration validator~~
→ `tests/Testurio.UnitTests/Validators/ReportConfigurationValidatorTests.cs` ✅

T028. `[Test]` Integration tests for template upload/remove API endpoints
→ `tests/Testurio.IntegrationTests/Controllers/ProjectSettingsControllerTests.cs`

T029. `[Test]` Integration tests for report rendering with custom templates and attachment toggles
→ `tests/Testurio.IntegrationTests/Plugins/ReportWriterPluginTests.cs`

T030. `[Test]` Frontend component tests for ReportTemplateUpload validation and warnings
→ `source/portal/src/components/ReportTemplateUpload/ReportTemplateUpload.test.tsx`

T031. `[Test]` Frontend component tests for ReportAttachmentToggles interaction and test_type coercion
→ `source/portal/src/components/ReportAttachmentToggles/ReportAttachmentToggles.test.tsx`

T032. `[Test]` Frontend hooks tests for useReportSettings queries and mutations
→ `source/portal/src/hooks/useReportSettings.test.ts`

## Rationale

**Dependency Chain**
Feature 0009 depends on completed feature 0004 (Test Report Delivery), which provides the base ReportDeliveryResult and integration with Jira/ADO. Feature 0005 (Execution Log Capture) is planned and will provide execution logs that can be optionally included in rendered templates via the `{{logs}}` token. Feature 0006 (Project Creation) is planned and provides the core project CRUD upon which this feature extends configuration fields.

**Task Ordering Logic**

Tasks are ordered following the three-layer backend sequence and two-layer frontend sequence:

1. **Domain Layer (T001–T002)**: Schema changes to Project entity and new interfaces must be defined first. The `reportTemplateUri`, `reportIncludeLogs`, and `reportIncludeScreenshots` fields are added to Project; the IBlobStorageClient interface is defined.

2. **Infrastructure Layer (T003–T005)**: Blob Storage client is implemented before it is registered in DI (T004), then the template repository is built on top of the client. This enables the template service and plugin to depend on a working storage abstraction.

3. **Application Service Layer (T006–T007)**: DTOs for upload requests/responses and the ReportTemplateService are created. The service encapsulates file validation (extension, size), UTF-8 encoding checks, and token scanning against the supported token list. This is a pure, stateless service that ReportWriterPlugin will later use.

4. **Report Rendering Layer (T009–T012)**: The ReportBuilderService is extended to accept custom templates. A new TemplateRenderer service handles placeholder token substitution with safe fallback (unknown tokens left as-is, missing data replaced with empty string). A DefaultReportTemplate constant provides the built-in template. The ReportWriterPlugin is updated to fetch the custom template (or fall back to default), render it, and store the rendered report as a blob in Azure Blob Storage, persisting its URI on the TestRun document.

5. **Validation & Configuration (T013)**: A ReportConfigurationValidator ensures that `reportIncludeScreenshots` cannot be true when `test_type` is `api`, enforced at the API boundary.

6. **API Layer (T008, T014–T015)**: Three endpoints are added under the project settings route group:
   - POST `/v1/projects/{id}/report-settings/template` (upload or replace with old blob cleanup)
   - DELETE `/v1/projects/{id}/report-settings/template` (remove with blob deletion)
   - GET `/v1/projects/{id}/report-settings` (fetch current state)
   - PATCH `/v1/projects/{id}/report-settings` (update attachment toggles)

7. **Frontend Layer (T016–T024)**: Types, services, hooks, and MSW mocks are created in sequence. Components are built atop hooks, and both integrate into the project settings section. Translation keys are added last, after component structure is fixed.

8. **Test Layer (T025–T032)**: Backend unit tests cover validation and token scanning; integration tests cover the full template lifecycle (upload → render → post). Frontend tests cover component interaction, hook state, and validation feedback.

**Cross-Feature Dependencies**

- **Feature 0004**: Provides ReportDeliveryResult and IJiraApiClient. Feature 0009 extends report delivery by storing rendered reports as blobs and changing the comment body from a hard-coded format to the custom template output.
- **Feature 0005**: When implemented, will populate `{{logs}}` token in rendered templates. Until then, the token substitutes to empty string if `reportIncludeLogs` is true and logs are not available (AC-018).
- **Feature 0006**: Provides project CRUD; feature 0009 adds report-related fields to the project configuration.

**Architectural Decisions**

- **Blob Storage over Database**: Report templates and rendered reports are stored in Azure Blob Storage, not Cosmos DB, for scalability and separation of concerns. Only blob URIs are persisted in Cosmos DB, following the existing pattern (screenshots, artifacts).
- **Template URI vs Inline Template**: The template file is stored once in blob storage and referenced by URI on the project document. This avoids duplicate storage and allows efficient template updates (delete old blob, upload new, update URI).
- **Atomic Project Updates**: When a template is replaced, the new blob is uploaded first, then the old blob URI is updated on the project document, then the old blob is deleted. If the deletion fails, the orphaned blob is flagged for cleanup (AC-014).
- **Safe Token Substitution**: Unknown tokens are left as-is; missing data is replaced with empty string. This prevents silent report corruption from typos and allows QA leads to catch mistakes during upload validation.
- **Default Built-In Template**: Every run has a fallback report template (AC-030), supporting all placeholder tokens. If a custom template cannot be fetched at render time, the built-in template is used (AC-034).
- **Feature Flag Not Required**: Report template rendering is not behind a feature flag; all projects get the new behavior by default (custom template if uploaded, built-in otherwise). This is backward-compatible because the built-in template produces the same output as the current hard-coded ReportBuilderService.

**Coercion Behavior**

When a user changes `test_type` from `ui_e2e` or `both` to `api`, the `reportIncludeScreenshots` toggle is automatically coerced to false and persisted on save (AC-024). This is handled client-side in the ReportAttachmentToggles component and validated server-side by the ReportConfigurationValidator.

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
