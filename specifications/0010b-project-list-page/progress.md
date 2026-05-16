# Progress — Projects List Page (0010b)

## Phase Status

| Phase     | Status      | Date       | Notes |
| --------- | ----------- | ---------- | ----- |
| Specify   | ✅ Complete | 2026-05-15 |       |
| Plan      | ✅ Complete | 2026-05-15 |       |
| Implement | ✅ Complete | 2026-05-15 |       |
| Review    | ✅ Complete | 2026-05-15 |       |
| Test      | ✅ Complete | 2026-05-16 |       |

---

## Implementation Notes

_Populated by `/implement [####]`_

---

## Review — 2026-05-15

### Blockers fixed
- `source/Testurio.Web/e2e/projects-list.spec.ts` — T010 E2E test file was missing; created with tests for sorted card grid, empty state CTA navigation, card click to history, and edit icon click to settings

### Warnings fixed
- `source/Testurio.Web/src/components/ProjectListCard/ProjectListCard.tsx:83` — `getStyles` called `useMemo` internally outside a component body (Rules of Hooks violation); refactored to a plain function and moved `useMemo` call into the component
- `source/Testurio.Web/src/views/ProjectsPage/ProjectsPage.tsx:117` — same `getStyles`/`useMemo` Rules of Hooks violation; same fix applied
- `source/Testurio.Web/src/components/ProjectListCard/ProjectListCard.test.tsx:16` — i18n test resources used flat dot-notation keys (`'card.editAriaLabel'`) instead of nested objects matching the actual `projects.json` structure; corrected to nested format
- `source/Testurio.Web/src/views/ProjectsPage/ProjectsPage.test.tsx:33` — same flat key issue across all `projects` namespace keys; corrected to nested format

### Suggestions fixed
- `source/Testurio.Web/src/routes/routes.ts` — added `NEW_PROJECT_ROUTE = '/projects/new'` constant so `ProjectsPage` can import it from the canonical route registry instead of defining a local string
- `source/Testurio.Web/src/views/ProjectsPage/ProjectsPage.tsx:18` — removed local `NEW_PROJECT_ROUTE` constant and imported from `routes.ts`

### Status: Complete

---

## Test Results

### Execution Date: 2026-05-16

**Unit tests — 6/6 passed** (`truncateText.test.ts`)
**Component tests — 15/15 passed** (`ProjectListCard.test.tsx`, `ProjectsPage.test.tsx`)
**E2E tests — 4/4 passed** (`e2e/projects-list.spec.ts`, Playwright 1.58.0, Chromium)

All acceptance criteria covered:
- AC-003: Sort order ✅ (E2E)
- AC-004: Truncation at 120 chars ✅ (unit)
- AC-009/010: Empty state CTA ✅ (E2E)
- AC-013: Card → history navigation ✅ (E2E)
- AC-016/018: Edit icon → settings navigation, no history propagation ✅ (E2E)
- AC-022–025: Backend (JWT auth, userId scoping, soft-delete, empty array) ✅ covered by feature 0006 integration tests

Backend `GET /v1/projects` was fully implemented by feature 0006 — no new backend tasks needed.

---

## Amendments

_Populated when spec or plan changes after initial approval. Format:_

```
### Amendment — YYYY-MM-DD
**Changed**: [which documents were updated]
**Reason**: [why the change was needed]
**Impact**: [phases that need to re-run as a result]
```
