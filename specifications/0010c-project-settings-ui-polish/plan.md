# Implementation Plan — Project Settings Page — UI Polish & Tab Layout (0010c)

## Tasks

- [x] T001 [UI] Expose `ProjectFormHandle` (imperative ref: `triggerSubmit(): Promise<boolean>`, `isDirty: boolean`) via `forwardRef` + `useImperativeHandle`; remove save button in edit mode; normalise section title to `h6`; remove `maxWidth: 640` — `source/Testurio.Web/src/components/ProjectForm/ProjectForm.tsx`
- [x] T002 [UI] Expose `ReportSettingsSectionHandle` (imperative ref: `getValues()`, `isDirty: boolean`) via `forwardRef` + `useImperativeHandle`; remove standalone save button — `source/Testurio.Web/src/views/ProjectSettings/ReportSettingsSection.tsx`
- [x] T003 [UI] Create `SaveBar` component — single "Save Changes" / "No changes" / "Saving…" / "Saved ✓" button with dirty+loading+success states — `source/Testurio.Web/src/components/SaveBar/SaveBar.tsx`
- [x] T004 [UI] Add translation keys for tabs, save bar states, and unsaved-changes banner — `source/Testurio.Web/src/locales/en/project.json`
- [x] T005 [UI] Refactor `ProjectSettingsPage` — add MUI `Tabs`/`Tab` navigation, wire imperative refs to `handleSaveAll`, implement dirty-state aggregation, unsaved-changes banner, per-section error alerts, retry-only-failed logic — `source/Testurio.Web/src/views/ProjectSettingsPage/ProjectSettingsPage.tsx`
- [x] T006 [Test] Component tests for `SaveBar` (all button states) — `source/Testurio.Web/src/components/SaveBar/SaveBar.test.tsx`
- [x] T007 [Test] Component tests for updated `ProjectSettingsPage` (tab switching, unified save, unsaved-changes banner, partial failure) — `source/Testurio.Web/src/views/ProjectSettingsPage/ProjectSettingsPage.test.tsx`
- [ ] T008 [Test] E2E — tab navigation, unified save happy path, partial failure retry — `source/Testurio.Web/e2e/project-settings-ui-polish.spec.ts` — `source/Testurio.Web/e2e/project-settings-ui-polish.spec.ts`

## Rationale

**T001 and T002 before T005** — `ProjectSettingsPage` depends on the imperative handles exposed by `ProjectForm` and `ReportSettingsSection`. The refs and handle types must exist before the page component can wire them up.

**T003 before T005** — `SaveBar` is a presentational component consumed by `ProjectSettingsPage`. It must be created before the page is refactored.

**T004 before T005** — translation keys must exist before the page renders localised strings (missing keys would silently fall back to the key string in development).

**T005 last among UI tasks** — it orchestrates everything: tab layout, dirty aggregation, unified save, retry logic, and the unsaved-changes banner. All building blocks (T001–T004) must be in place.

**T006 and T007 after their respective subjects** — tests target concrete component APIs. Writing them before the implementation would require constant rework.

**T008 last** — E2E tests exercise the fully assembled page with real tab navigation and save flows; they depend on every UI layer being complete.

**No backend tasks** — this feature is purely a frontend refactor. All API contracts remain unchanged; only component structure, state management, and visual layout are affected.

**Retry-only-failed design** — when multiple API calls are made and one fails, `ProjectSettingsPage` tracks which sections succeeded (by storing their last-saved snapshot) and which remain dirty. On retry, only the dirty sections are re-posted. This avoids redundant writes and gives the user accurate per-section error attribution.

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
