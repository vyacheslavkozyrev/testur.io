# Progress — Project Dashboard (0010)

## Phase Status

| Phase     | Status      | Date       | Notes |
| --------- | ----------- | ---------- | ----- |
| Specify   | ✅ Complete | 2026-05-09 |       |
| Plan      | ✅ Complete | 2026-05-09 |       |
| Implement | ✅ Complete | 2026-05-16 |       |
| Review    | ✅ Complete | 2026-05-16 |       |
| Test      | ✅ Complete | 2026-05-16 |       |

---

## Implementation Notes

_Populated by `/implement [####]`_

---

## Review — 2026-05-16

### Blockers fixed
- `source/Testurio.Infrastructure/DependencyInjection.cs`:9–12 — duplicate `using Testurio.Infrastructure.Cosmos;` directive introduced by the new StatsRepository registration; removed the extra using to restore compilation.

### Warnings fixed
- `source/Testurio.Api/Services/DashboardService.cs`:24–30 — sequential `await` of two independent repository calls added unnecessary latency; replaced with `Task.WhenAll` to run both Cosmos queries in parallel.

### Suggestions fixed
- `source/Testurio.Web/src/components/ProjectCard/ProjectCard.test.tsx`:88–89 — duplicate `expect(screen.getByText('Never run')).toBeInTheDocument()` assertion provided no coverage; replaced the second line with `expect(screen.queryByText(/Last run:/)).not.toBeInTheDocument()` to actually verify the timestamp is absent when `latestRun` is null (per AC-002 / T025 intent).

### Status: Complete

---

## Test Results

### Run — 2026-05-16

**Backend unit tests (T022):** 6 / 6 passed — `DashboardServiceTests`

**Backend integration tests (T023):** 5 / 5 passed — `StatsControllerTests`
- Fixed: `ReadFromJsonAsync` options in two tests were missing `JsonStringEnumConverter`, causing deserialization failures for the `RunStatus` enum serialized as a string. Added shared `JsonOptions` instance with the converter.

**Frontend component tests (T024–T027):** 28 / 28 passed — `RunStatusBadge`, `ProjectCard`, `QuotaUsageBar`, `DashboardPage`
- Fixed: `ProjectCard` tests used `getByText('Never run')` which failed when the text appeared twice (once in the Chip badge, once in the timestamp fallback Typography). Switched to `getAllByText`.
- Fixed: Components called `useMemo(() => getStyles(theme), [theme])` where `getStyles` itself contains `useMemo` — a nested Hook call that crashes in the browser (though passes in jsdom). Changed all affected components to call `getStyles(theme)` directly. Affected files: `DashboardPage.tsx`, `ProjectCard.tsx`, `QuotaUsageBar.tsx`, `RunStatusBadge.tsx`, `ProjectsPage.tsx`, `ProjectListCard.tsx`.

**E2E tests (T028):** 7 / 7 passed — `dashboard.spec.ts` (created in this phase)
- Covers: AC-001 (route accessible), AC-003/AC-004 (sort order + badges), AC-007/AC-012 (Create Project button always visible), AC-009/AC-010/AC-011 (empty state CTA), AC-013/AC-014 (quota bar visible), AC-020/AC-021/AC-024 (card link href correct).

**Overall: Passed — all tests pass, all acceptance criteria covered.**

---

## Amendments

### Amendment — 2026-05-09
**Changed**: `stories.md` and `plan.md` fully rewritten
**Reason**: Clarified requirements introduced: richer 7-value `RunStatus` enum, SSE real-time updates (replacing polling), global quota bar (not per-card), card sort order by latest run activity, empty-state CTA panel, card navigation to `/projects/:id/history`, and navigation contract constants for feature 0011
**Impact**: Implement phase must use the rewritten plan; no implementation work had begun so no rework is required

### Amendment — 2026-05-11
**Changed**: Feature split — SSE real-time updates extracted into new feature 0043
**Reason**: The SSE concern (DashboardStreamManager, DashboardEventRelay, useDashboardStream hook, reconnect logic) is architecturally isolated and independently deliverable. The snapshot dashboard ships usable value without live updates.
**Impact**: US-003 and its tasks (IDashboardStreamManager, DashboardStreamManager, DashboardEventRelay, SSE endpoint, useDashboardStream, SSE tests) moved to feature 0043. Remaining tasks renumbered T001–T028. DashboardUpdatedEvent domain record and its TypeScript counterpart are retained here so feature 0043 can import without a reverse dependency.
