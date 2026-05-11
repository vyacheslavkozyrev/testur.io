# Progress — Project Creation & Core Configuration (0006)

## Phase Status

| Phase     | Status      | Date       | Notes |
| --------- | ----------- | ---------- | ----- |
| Specify   | ✅ Complete | 2026-05-09 |       |
| Plan      | ✅ Complete | 2026-05-10 |       |
| Implement | ✅ Complete | 2026-05-10 |       |
| Review    | ✅ Complete | 2026-05-10 |       |
| Test      | ⏳ Pending  |            |       |

---

## Implementation Notes

All 20 tasks completed (T001–T020; T021 E2E tests deferred as out of scope for this phase).

- Project entity updated with core fields (name, productUrl, testingStrategy, isDeleted, timestamps); Jira fields made nullable for future feature 0007 integration.
- Full CRUD added to IProjectRepository interface and implemented in ProjectRepository (Cosmos DB).
- Key Vault namespace helper (ProjectSecretNamespace) added for future secret naming.
- ProjectService handles create, list, get, update, and soft-delete with compile-time LoggerMessage logging.
- Minimal API endpoints at /v1/projects with RequireAuthorization, ValidationFilter, and GlobalExceptionHandler returning proper ProblemDetails.
- Frontend: TypeScript types, projectService, useProject hooks, MSW handlers, ProjectForm, ProjectDeleteDialog, ProjectSettingsPage, i18n translations, and route registration.
- Backend unit tests (9 tests) and integration tests (12 tests) all pass.

---

## Review — 2026-05-10

### Blockers fixed
- `specifications/0006-project-creation-core-configuration/plan.md`:T002,T003 — File paths recorded as `source/Testurio.Core/Interfaces/IProjectRepository.cs` and `source/Testurio.Infrastructure/Repositories/ProjectRepository.cs`; actual locations are `source/Testurio.Core/Repositories/IProjectRepository.cs` and `source/Testurio.Infrastructure/Cosmos/ProjectRepository.cs`. Corrected.

### Warnings fixed
- `source/Testurio.Api/Services/ProjectService.cs`:UpdateAsync,DeleteAsync — AC-018 and AC-031 require 403 Forbidden when a project exists but belongs to a different user. The original implementation returned 404 in both cases because `GetByIdAsync` scopes by partition (userId) and returns null for cross-user access. Fixed by adding a `ProjectOperationResult` discriminated enum and using `GetByProjectIdAsync` to distinguish ownership before mutating, returning `Forbidden` when `project.UserId != userId`.
- `source/Testurio.Api/Endpoints/ProjectEndpoints.cs`:UpdateProjectAsync,DeleteProjectAsync — Updated endpoints to map `ProjectOperationResult.Forbidden` to `TypedResults.Forbid()` using switch expression. `DeleteProjectAsync` return type updated to include `ForbidHttpResult`.
- `source/Testurio.Api/Middleware/ValidationFilter.cs`:13 — When the request body is missing/unparseable, `OfType<T>().FirstOrDefault()` returns null and validation was silently skipped. Fixed to return `TypedResults.ValidationProblem` with a `body` field error when target is null.
- `source/Testurio.Web/src/pages/ProjectSettingsPage/ProjectSettingsPage.tsx`:handleDeleteConfirm — Dialog was not closed before `navigate('/projects')` on successful deletion. Added `setDeleteDialogOpen(false)` before navigation so the dialog dismisses cleanly if navigation is delayed.

### Suggestions fixed
- `source/Testurio.Web/src/components/ProjectDeleteDialog/ProjectDeleteDialog.tsx` — Removed superfluous `useCallback` wrappers (`handleConfirm`/`handleCancel`) that wrapped prop functions with no transformation. Props (`onConfirm`, `onCancel`) passed directly per the ui.md rule that `useCallback` is only needed when the handler is passed further down as a new prop reference.

### Remaining issues (if any)
None.

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
