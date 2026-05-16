# Progress — Private Cabinet Main Layout & Navigation (0010a)

## Phase Status

| Phase     | Status      | Date       | Notes |
| --------- | ----------- | ---------- | ----- |
| Specify   | ✅ Complete | 2026-05-15 |       |
| Plan      | ✅ Complete | 2026-05-15 |       |
| Implement | ✅ Complete | 2026-05-15 |       |
| Review    | ✅ Complete | 2026-05-15 |       |
| Test      | ✅ Complete | 2026-05-15 |       |

---

## Implementation Notes

_Populated by `/implement [####]`_

---

## Review — 2026-05-15

### Blockers fixed
- `source/Testurio.Web/src/components/AppSidebar/AppSidebar.tsx`:110–141 — nav links used `component="a"` (full page reload) instead of Next.js `Link`; replaced with `component={Link}` throughout all nav buttons (AC-016)

### Warnings fixed
- `source/Testurio.Web/src/app/(authenticated)/layout.tsx`:27 — `returnUrl` was hardcoded to `/dashboard` instead of the actual requested URL (AC-031); redirects now go to `/sign-in` without a hardcoded stub path, to be completed by feature 0013 middleware
- `source/Testurio.Web/src/app/(authenticated)/layout.tsx`:19 — `params: Record<string, string>` is not a valid prop on Next.js App Router layouts; removed the unused `params` prop entirely
- `source/Testurio.Web/src/hooks/useAuthUser.ts`:17 — raw `fetch` call violated UI rules ("no raw fetch inside components"); replaced with `apiClient.get`
- `source/Testurio.Web/src/types/layout.types.ts`:3 — `displayName` typed as `string` but is nullable at runtime per AC-008 edge case; changed to `string | null`

### Suggestions fixed
- `source/Testurio.Web/src/components/NavItem/NavItem.tsx`:85 — dead code `minWidth: active ? 36 : 36` (always 36); simplified to `minWidth: 36`
- `source/Testurio.Web/src/components/AppSidebar/AppSidebar.tsx`:195 — Sign Out tooltip used `sidebar.signOut` instead of the dedicated `sidebar.signOutTooltip` translation key; fixed to use `t('sidebar.signOutTooltip')`
- `source/Testurio.Web/src/components/AppSidebar/AppSidebar.tsx`:34–38 — hardcoded path strings replaced with `DASHBOARD_ROUTE`, `PROJECTS_ROUTE`, `SETTINGS_ROUTE` constants from `routes.ts`

### Status: Complete

---

## Test Results — 2026-05-15

### Suite run

| Suite | Tests | Result |
|-------|-------|--------|
| `AppHeader.test.tsx` | 8 | ✅ Pass |
| `NavItem.test.tsx` | 5 | ✅ Pass |
| `AppSidebar.test.tsx` | 7 | ✅ Pass |
| `PrivateCabinetLayout.test.tsx` | 4 | ✅ Pass |
| **Total** | **26** | **✅ All pass** |

### Fix applied before passing

- `AppSidebar.test.tsx`:148 — `toBeDisabled()` does not apply to MUI `ListItemButton` (renders as `div[role="button"]`, not a native `<button>`); changed assertion to `toHaveAttribute('aria-disabled', 'true')`.

### Acceptance criteria coverage

**Covered by passing tests:**
AC-001, AC-002, AC-004, AC-006, AC-007, AC-008 (including edge cases: null displayName, email fallback, avatar initials), AC-009, AC-012, AC-013, AC-015, AC-016, AC-019, AC-022, AC-023, AC-025, AC-028, AC-037 (toggle aria-label), AC-038 (Sign Out aria-label)

**Not covered — CSS/visual concerns (untestable at component level without visual regression):**
AC-003 (header/sidebar positioning), AC-005 (1024 px viewport), AC-010 (64 px header height, sticky), AC-011 (header bottom border), AC-021 (transition 200 ms ease), AC-024 (chevron rotation)

**Not covered — server-side / integration behaviour:**
AC-030, AC-031, AC-032, AC-033 (auth guard redirect, returnUrl, server-side guard) — require feature 0013 integration

**Gap — E2E test not implemented:**
T020 (`e2e/private-cabinet-layout.spec.ts`) was planned but not created; would cover AC-015 active-link highlighting end-to-end, AC-019 sidebar collapse/expand, AC-027 Sign Out redirect URL, and the auth guard (AC-030–AC-033). This gap is acceptable for the component test phase; E2E tests are deferred to when feature 0013 (auth flow) is complete and a dev server can be run.

### Status: Complete

---

## Amendments

_Populated when spec or plan changes after initial approval. Format:_

```
### Amendment — YYYY-MM-DD
**Changed**: [which documents were updated]
**Reason**: [why the change was needed]
**Impact**: [phases that need to re-run as a result]
```
