# Progress — Project Dashboard (0010)

## Phase Status

| Phase     | Status      | Date       | Notes |
| --------- | ----------- | ---------- | ----- |
| Specify   | ✅ Complete | 2026-05-09 |       |
| Plan      | ✅ Complete | 2026-05-09 |       |
| Implement | ✅ Complete | 2026-05-16 |       |
| Review    | ✅ Complete | 2026-05-16 |       |
| Test      | ⏳ Pending  |            |       |

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

_Populated by `/test [####]`_

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
