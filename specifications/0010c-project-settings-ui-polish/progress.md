# Progress — Project Settings Page — UI Polish & Tab Layout (0010c)

## Phase Status

| Phase     | Status      | Date       | Notes |
| --------- | ----------- | ---------- | ----- |
| Specify   | ✅ Complete | 2026-05-16 |       |
| Plan      | ✅ Complete | 2026-05-16 |       |
| Implement | ✅ Complete | 2026-05-16 |       |
| Review    | ✅ Complete | 2026-05-16 |       |
| Test      | ✅ Complete | 2026-05-16 |       |

---

## Implementation Notes

_Populated by `/implement 0010c`_

---

## Review

### Findings and Fixes (2026-05-16)

**BLOCKER — ReportSettingsSection: dead `useUpdateReportSettings` instance, `isDirty` never resets after parent save**
- `ReportSettingsSection` called `useUpdateReportSettings` internally, but the parent owns the mutation. The child
  instance's `isSuccess` would never fire, so `pendingLogs`/`pendingScreenshots` were never cleared after a
  successful save, causing `isDirty` to remain `true` indefinitely.
- **Fix:** Removed the internal `useUpdateReportSettings` call. Added `clearDirty()` to `ReportSettingsSectionHandle`;
  parent calls it immediately after `updateReportSettings.mutateAsync` succeeds. Added a safety-net `useEffect`
  that also clears pending state when the server data object refreshes via React Query cache invalidation.

**BLOCKER — AC-024: Missing `h6` section heading for the project info card**
- The `form.titleEdit` translation key existed in `project.json` but was never rendered anywhere in
  `ProjectSettingsPage`. AC-024 requires the "Edit Project" section title to use `h6` variant.
- **Fix:** Added `<Typography variant="h6" sx={styles.cardTitle}>{t('form.titleEdit')}</Typography>` at the
  top of the project info card. Added `cardTitle` style to `getStyles`. Added `display: flex / flexDirection:
  column / gap` to the `card` style so the heading and form field stack correctly.

**WARNING — Inline arrow function passed as prop (ui.md rule)**
- `onClick={() => setActiveTab('settings')}` was used directly inside the `Alert` action prop.
- **Fix:** Extracted to `handleGoToSettings` `useCallback` and referenced by name.

**WARNING — `setTimeout` without cleanup in `handleSaveAll`**
- `setTimeout(() => setSaveBarState('clean'), 2000)` had no cleanup, risking a setState call on an unmounted
  component if the user navigated away within 2 seconds of a successful save.
- **Fix:** Captured the timer id (`const timer = window.setTimeout(...)`) and returned a cleanup
  `() => window.clearTimeout(timer)` from `handleSaveAll`.


---

## Test Results

### Run: 2026-05-16

**Jest — component tests**
- 18 test suites, 165 tests — all passed (13.14 s)
- Relevant suites for this feature:
  - `SaveBar.test.tsx` — PASS (all button states: clean, dirty, saving, saved)
  - `ProjectSettingsPage.test.tsx` — PASS (tab switching, unified save, unsaved-changes banner, partial failure)
  - `ProjectForm.test.tsx` — PASS (imperative handle, validation)
- `console.error` warnings about `act(...)` in `ProjectSettingsPage.test.tsx` are a known React 18 / JSDOM async
  environment warning; they do not indicate test failures.
- T008 (E2E spec) is explicitly marked as pending in `plan.md` and is out of scope for this test phase.

**TypeScript (`tsc --noEmit`)**
- Exit code 2 — errors are all `TS2582 / TS2304 / TS2708` ("Cannot find name 'describe'/'expect'/jest") in
  `*.test.tsx` files because `tsconfig.json` does not include `@types/jest` (test types are resolved by Jest's
  own transform, not `tsc`). This is a pre-existing project-wide configuration issue, not introduced by this
  feature. No errors in production source files.

**Acceptance Criteria Verification**

| AC    | Status | Evidence |
|-------|--------|----------|
| AC-001 | ✅ | `<Tabs value={activeTab}><Tab value="settings"/><Tab value="integration"/>` in `ProjectSettingsPage.tsx` |
| AC-002 | ✅ | `{activeTab === 'integration' && <IntegrationPage embedded />}` |
| AC-003 | ✅ | `{activeTab === 'settings' && <Box sx={styles.settingsContent}>…</Box>}` |
| AC-004 | ✅ | MUI `Tabs` highlights active tab by default |
| AC-005 | ✅ | `t('tabs.settings')` / `t('tabs.integration')` — keys present in `project.json` |
| AC-006 | ✅ | Single `<SaveBar>` at bottom of settings content; no other Save button in settings tab |
| AC-007 | ✅ | `ProjectForm` renders save button only in create mode (`{!isEdit && …}`); `ReportSettingsSection` has no save button |
| AC-008 | ✅ | `handleSaveAll` calls `updateProject.mutateAsync` then `updateReportSettings.mutateAsync` sequentially |
| AC-009 | ✅ | `triggerSubmit()` resolves `false` on validation failure; `handleSaveAll` returns early before any API call |
| AC-010 | ✅ | `SaveBar` shows `CircularProgress` and is disabled when `state === 'saving'` |
| AC-011 | ✅ | `SaveBar` disabled when `state === 'clean'`; label shows "No changes" |
| AC-012 | ✅ | On success: `setSaveBarState('saved')` + `window.setTimeout(…'clean', 2000)`; button color `success` when saved |
| AC-013 | ✅ | Returns to "Save Changes" (enabled) when `computeDirty()` fires after a user edit |
| AC-014 | ✅ | Per-section `<Alert severity="error">` rendered when `sectionErrors.projectInfo` or `sectionErrors.reportSettings` |
| AC-015 | ✅ | Error alerts are conditional on individual section flags |
| AC-016 | ✅ | `setSaveBarState('dirty')` on any error path |
| AC-017 | ✅ | `pendingSections` tracks which sections still need saving; skips sections where `pendingSections.x === false` |
| AC-018 | ✅ | `computeDirty()` aggregates form dirty, report dirty, prompt dirty; effect sets `saveBarState('dirty')` |
| AC-019 | ✅ | `SaveBar` label "No changes" when `state === 'clean'`; button disabled |
| AC-020 | ✅ | `{activeTab === 'integration' && hasDirtySettings && <Alert …>}` with Go-to-Settings button |
| AC-021 | ✅ | Banner only shown when `hasDirtySettings` is true; disappears after save |
| AC-022 | ✅ | `ProjectForm` `getStyles`: `root` has `width: '100%'`, no `maxWidth` constraint |
| AC-023 | ✅ | All sections in same `<Box sx={styles.settingsContent}>` column, no individual width overrides |
| AC-024 | ✅ | Project info card renders `<Typography variant="h6">` with `t('form.titleEdit')` |
| AC-025 | ✅ | No heading exceeds `h6` in the Settings tab |

---

## Amendments

_Populated when spec or plan changes after initial approval._
