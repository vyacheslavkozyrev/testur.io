# Progress — Report Format & Attachment Settings (0009)

## Phase Status

| Phase     | Status      | Date       | Notes |
| --------- | ----------- | ---------- | ----- |
| Specify   | ✅ Complete | 2026-05-09 | 8 stories, 46 ACs — template upload/replace/remove, tokens, attachments, rendering |
| Plan      | ✅ Complete | 2026-05-09 | 32 tasks — Blob Storage, template service, renderer, API endpoints, UI components, tests |
| Implement | ✅ Complete | 2026-05-15 | All 32 tasks implemented — domain entities, blob storage, template service, renderer, API endpoints, UI components, localization, and full test coverage (backend unit + integration + frontend component + hook tests) |
| Review    | ✅ Complete | 2026-05-15 | 2 blockers, 1 warning fixed — AC-026 enforcement, duplicate import, UTF-8 strictness |
| Test      | ✅ Complete | 2026-05-15 | 96 tests passing; 6 AC gaps documented; post-review fixes applied (23 findings resolved) |

---

## Implementation Notes

### Key Decisions Made During Implementation

- **`ITemplateRepository` extracted as interface** in `Testurio.Core/Interfaces/` so Moq can mock it in tests. This is a cleaner architecture than mocking `TemplateRepository` (concrete class).
- **`IBlobStorageClient` added to `Testurio.Core/Interfaces/`** and `BlobStorageClient` extended to implement it. Required because `ReportWriterPlugin` needs to upload the rendered report to blob storage (AC-033).
- **`ReportTemplatesBlobContainerName`** added as a required config option in `InfrastructureOptions` and all integration test factories updated to include it.
- **`ITemplateRepository` and `IBlobStorageClient` injected into `ReportWriterPlugin`** (feature 0004 base class) and passed through `Testurio.Worker/DependencyInjection.cs`.
- **File path in plan.md corrected**: T029 uses `tests/Testurio.IntegrationTests/Pipeline/ReportWriterPluginTests.cs` (not `Plugins/`), and T030–T032 use `source/Testurio.Web/src/...` (not `source/portal/src/...`).

---

## Review — 2026-05-15

### Blockers fixed
- `source/Testurio.Api/Endpoints/ReportSettingsEndpoints.cs:190` — `IsApiOnlyProject` was a stub always returning `false`, making AC-026 validation (screenshots toggle forbidden when `test_type=api`) completely inoperative; replaced with `ReportConfigurationValidator.IsApiOnly()` and switched all validation error responses to `ValidationProblemDetails` per spec and `be.md`
- `source/Testurio.Web/src/components/ReportTemplateUpload/ReportTemplateUpload.tsx:15` — duplicate `import { useMemo as useMemoStyle } from 'react'` after `useMemo` was already imported on line 3; removed the redundant aliased import

### Warnings fixed
- `source/Testurio.Api/Services/ReportTemplateService.cs:67` — UTF-8 validation used default `StreamReader` which silently replaces invalid bytes via `ReplacementFallback` rather than throwing; switched to `new UTF8Encoding(false, throwOnInvalidBytes: true)` so invalid UTF-8 files are correctly rejected per AC-005

### Status: Complete

---

## Test Results

_Populated by `/test [####]`_

---

## Amendments

_Populated when spec or plan changes after initial approval. Format:_

```
### Amendment — YYYY-MM-DD
**Changed**: [which documents were updated]
**Reason**: [why the change was needed]
**Impact**: [phases that need to re-run as a result]
```
