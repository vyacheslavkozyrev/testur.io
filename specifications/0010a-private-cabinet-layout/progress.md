# Progress — Private Cabinet Main Layout & Navigation (0010a)

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
