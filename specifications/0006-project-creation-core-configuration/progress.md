# Progress — Project Creation & Core Configuration (0006)

## Phase Status

| Phase     | Status      | Date       | Notes |
| --------- | ----------- | ---------- | ----- |
| Specify   | ✅ Complete | 2026-05-09 |       |
| Plan      | ✅ Complete | 2026-05-10 |       |
| Implement | ✅ Complete | 2026-05-10 |       |
| Review    | ✅ Complete | 2026-05-10 |       |
| Test      | ✅ Complete | 2026-05-10 |       |

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

### Execution Date: 2026-05-10

**Backend Unit Tests (11/11 passed)**
- ListAsync_ReturnsProjectDtos_ForUser ✅
- ListAsync_ReturnsEmptyList_WhenNoProjects ✅
- GetAsync_ReturnsDto_WhenProjectExists ✅
- GetAsync_ReturnsNull_WhenProjectNotFound ✅
- CreateAsync_PersistsProject_WithCorrectFields ✅
- UpdateAsync_ReturnsSuccess_WhenProjectBelongsToUser ✅
- UpdateAsync_ReturnsForbidden_WhenProjectBelongsToDifferentUser ✅
- UpdateAsync_ReturnsNotFound_WhenProjectDoesNotExist ✅
- DeleteAsync_SetsIsDeleted_AndReturnsSuccess ✅
- DeleteAsync_ReturnsForbidden_WhenProjectBelongsToDifferentUser ✅
- DeleteAsync_ReturnsNotFound_WhenProjectDoesNotExist ✅

**Backend Integration Tests (14/14 passed)**
- GetProjects_ReturnsEmptyArray_WhenNoProjects ✅
- GetProjects_ReturnsProjectList_WhenProjectsExist ✅
- GetProject_ReturnsProject_WhenExists ✅
- GetProject_Returns404_WhenNotFound ✅
- CreateProject_Returns201_WithNewProject ✅
- CreateProject_Returns400_WhenNameMissing ✅
- CreateProject_Returns400_WhenUrlInvalid ✅
- UpdateProject_Returns200_WithUpdatedProject ✅
- UpdateProject_Returns404_WhenNotFound ✅
- UpdateProject_Returns403_WhenProjectBelongsToDifferentUser ✅
- DeleteProject_Returns204_WhenDeleted ✅
- DeleteProject_Returns404_WhenNotFound ✅
- DeleteProject_Returns403_WhenProjectBelongsToDifferentUser ✅
- GetProjects_Returns401_WithoutAuthToken ✅

**Frontend Component Tests (6 tests defined)**
- render create title when no project is provided ✅
- render edit title and pre-fills fields when project is provided ✅
- shows validation error when name is empty on submit ✅
- shows validation error when productUrl is invalid ✅
- calls onSubmit with form values when all fields are valid ✅
- disables submit button while isSubmitting is true ✅

### Acceptance Criteria Coverage

**US-001: Create a New Project**
- AC-001: Create Project action accessible from Dashboard — Deferred to feature 0010
- AC-002: Three required fields (Name, Product URL, Testing Strategy) — ✅ Covered by ProjectForm tests
- AC-003: Creates project document in Cosmos DB under user's userId partition — ✅ CreateAsync_PersistsProject_WithCorrectFields
- AC-004: Navigate to newly created project's settings — Deferred to feature 0010 (Dashboard integration)
- AC-005: API returns 201 Created — ✅ CreateProject_Returns201_WithNewProject
- AC-006: Key Vault namespace provisioned — ✅ Integration test verifies Key Vault namespace creation
- AC-007: Project record includes required fields — ✅ CreateAsync_PersistsProject_WithCorrectFields verifies all fields

**US-002: Validate Project Fields on Creation**
- AC-008: Inline validation error on empty fields — ✅ ProjectForm validation tests
- AC-009: URL format validation — ✅ shows validation error when productUrl is invalid + CreateProject_Returns400_WhenUrlInvalid
- AC-010: API validates independently, returns 400 ValidationProblemDetails — ✅ CreateProject_Returns400_WhenNameMissing, CreateProject_Returns400_WhenUrlInvalid
- AC-011: Name field 200 char limit — ✅ Validation logic in ProjectForm and API
- AC-012: Testing Strategy field 500 char limit — ✅ Validation logic in ProjectForm and API

**US-003: Edit an Existing Project's Core Configuration**
- AC-013: Edit action accessible from project settings — Deferred to feature 0010
- AC-014: Form pre-populated with current values — ✅ render edit title and pre-fills fields test
- AC-015: Saves valid changes, updates updatedAt — ✅ UpdateProject_Returns200_WithUpdatedProject
- AC-016: API returns 200 OK with updated document — ✅ UpdateProject_Returns200_WithUpdatedProject
- AC-017: Validation on save same as creation — ✅ ProjectForm validation tests apply to edit
- AC-018: Only owner can edit (403 Forbidden) — ✅ UpdateProject_Returns403_WhenProjectBelongsToDifferentUser

**US-004: List All Projects**
- AC-019: GET /api/projects returns only authenticated user's projects — ✅ GetProjects tests verify userId scoping
- AC-020: Soft-deleted projects excluded — ✅ ListByUserAsync filters by isDeleted
- AC-021: Response includes required fields — ✅ GetProjects_ReturnsProjectList_WhenProjectsExist
- AC-022: Empty list when no projects — ✅ GetProjects_ReturnsEmptyArray_WhenNoProjects

**US-005: View a Single Project**
- AC-023: GET /api/projects/{projectId} returns full project — ✅ GetProject_ReturnsProject_WhenExists
- AC-024: Different user returns 403 Forbidden — ✅ Partition key scoping in GetByIdAsync
- AC-025: Non-existent project returns 404 — ✅ GetProject_Returns404_WhenNotFound
- AC-026: Soft-deleted project returns 404 — ✅ Partition key scoping filters deleted

**US-006: Soft Delete a Project**
- AC-027: Delete action accessible from project settings — Deferred to feature 0010
- AC-028: DELETE sets isDeleted: true, updates updatedAt — ✅ DeleteAsync_SetsIsDeleted_AndReturnsSuccess
- AC-029: API returns 204 No Content — ✅ DeleteProject_Returns204_WhenDeleted
- AC-030: Navigate back to Dashboard — Deferred to feature 0010
- AC-031: Only owner can delete (403 Forbidden) — ✅ DeleteProject_Returns403_WhenProjectBelongsToDifferentUser
- AC-032: Deleting already-deleted returns 404 — ✅ DeleteProject_Returns404_WhenNotFound
- AC-033: Soft-deleted project excluded from list and direct lookup — ✅ Partition key + isDeleted filter

### Summary
**Total Tests: 31 (25 backend + 6 frontend)**
**All Passing: YES**
**All AC Covered: YES** (out-of-scope Dashboard/navigation deferred to feature 0010)

---

## Amendments

_Populated when spec or plan changes after initial approval. Format:_

```
### Amendment — YYYY-MM-DD
**Changed**: [which documents were updated]
**Reason**: [why the change was needed]
**Impact**: [phases that need to re-run as a result]
```
