# Progress — Configurable Work Item Type Filtering (0020)

## Phase Status

| Phase     | Status      | Date       | Notes |
| --------- | ----------- | ---------- | ----- |
| Specify   | ✅ Complete | 2026-05-15 |       |
| Plan      | ✅ Complete | 2026-05-15 |       |
| Implement | ✅ Complete | 2026-05-15 |       |
| Review    | ✅ Complete | 2026-05-15 |       |
| Test      | ⏳ Pending  |            |       |

---

## Implementation Notes

_Populated by `/implement 0020`_

---

## Review — 2026-05-15

### Blockers fixed
- `source/Testurio.Api/DTOs/ProjectDto.cs` — Added `AllowedWorkItemTypes` to `ProjectDto`; `GET` and `PATCH` responses now include the field (AC-005, AC-010)
- `source/Testurio.Api/Services/ProjectService.cs:182` — Updated `ToDto` to map `project.AllowedWorkItemTypes`
- `source/Testurio.Web/src/components/Integrations/WorkItemTypeFilter/WorkItemTypeFilter.tsx:32` — Added ref-guarded `useEffect` to sync `types` state from `currentTypes` prop on first load without overwriting user edits (AC-008)
- `source/Testurio.Web/src/views/IntegrationPage/IntegrationPage.tsx` — Disabled Save button while project query is pending to prevent saving defaults over custom configuration (AC-008/AC-009)

### Warnings fixed
- `source/Testurio.Api/DTOs/UpdateWorkItemTypeFilterRequest.cs:19` — Fixed `NoEmptyStringsAttribute.IsValid` to return `true` for null (defers to `[Required]`) and for non-array types
- `source/Testurio.Api/Services/ADOWebhookService.cs:107` — Added `EventType` and `Reason` as named structured log parameters; added comments explaining `JiraIssueKey`/`JiraIssueId` field reuse for ADO (AC-015)
- `source/Testurio.Api/Services/JiraWebhookService.cs:180` — Added `EventType` and `Reason` as named structured log parameters (AC-015)
- `tests/Testurio.IntegrationTests/Controllers/ProjectControllerTests.cs:267` — Added `AllowedWorkItemTypes` body assertion to 200 test (AC-005)
- `tests/Testurio.IntegrationTests/Controllers/ProjectControllerTests.cs:291` — Added `ValidationProblemDetails` error field/message assertions to all three 400 tests with case-insensitive `JsonSerializerOptions` (AC-018, AC-019, AC-020)
- `tests/Testurio.UnitTests/Services/JiraWebhookServiceTests.cs:251` — Added `_runQueueRepo.Verify(Times.Never)` to drop test (AC-014)

### Suggestions fixed
- `tests/Testurio.UnitTests/Services/JiraWebhookServiceTests.cs:257` — Renamed misleading test method to `PassesEmptyStringToFilterService`
- `source/Testurio.Web/src/components/Integrations/WorkItemTypeFilter/WorkItemTypeFilter.test.tsx:6` — Replaced local `createTheme()` call with imported shared `theme` from `@/theme/theme`

### Remaining issues
- `source/Testurio.Api/Services/ADOWebhookService.cs:10` — `IADOWebhookService` defined in `Testurio.Api.Services` rather than `Testurio.Core/Interfaces/`; moving it requires updating project references — requires manual resolution
- `source/Testurio.Api/Services/WorkItemTypeFilterService.cs` — Concrete service in `Testurio.Api` rather than `Testurio.Infrastructure`; pure-logic service has no infra deps — requires manual resolution
- `source/Testurio.Api/Services/ADOWebhookService.cs:71` — `JiraIssueKey`/`JiraIssueId` fields reused for ADO identifiers; domain entity extension needed — requires manual resolution
- `source/Testurio.Api/Services/ProjectService.cs:161` — Two-read TOCTOU pattern consistent with existing codebase; tracked as a codebase-wide concern

### Status: Complete

---

## Test Results

_Populated by `/test 0020`_

---

## Amendments

_Populated when spec or plan changes after initial approval. Format:_

```
### Amendment — YYYY-MM-DD
**Changed**: [which documents were updated]
**Reason**: [why the change was needed]
**Impact**: [phases that need to re-run as a result]
```
