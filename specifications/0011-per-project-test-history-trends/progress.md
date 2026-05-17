# Progress — Per-Project Test History & Trends (0011)

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

T001–T029 implemented across backend (Domain, Infra, App, API) and frontend (types, service, hooks, MSW, components, page, i18n, route). T030 (E2E) deferred to test phase. `@mui/x-charts` added as dependency for TrendChart.

---

## Review — 2026-05-16

### Blockers fixed
- `source/Testurio.Web/src/components/RunDetailPanel/RunDetailPanel.tsx:34` — `showRaw` state never reset when `runId` changes; added `useEffect` to reset to structured view on `runId` change (AC-031)
- `tests/Testurio.UnitTests/Services/ProjectHistoryServiceTests.cs:155` — `ScenarioSummary` named constructor call missing the required `ScenarioId` parameter; added `ScenarioId: "scenario-1"`
- `tests/Testurio.IntegrationTests/Controllers/StatsControllerTests.cs:258` — positional `ScenarioSummary` constructor called with 6 args but record has 7; added `"scenario-1"` as first positional arg

### Warnings fixed
- `source/Testurio.Web/src/types/history.types.ts:33` — `ScenarioSummary` TypeScript interface missing `scenarioId: string` field required by AC-021; added field and updated all test fixtures and mock handlers
- `source/Testurio.Web/src/components/RunDetailPanel/RunDetailPanel.tsx:133` — `<ScenarioCard>` list used array index as `key`; changed to `key={scenario.scenarioId}` for stable identity

### Status: Complete

---

## Test Results

### Backend — 2026-05-16

**Unit tests** (`Testurio.UnitTests/Services/ProjectHistoryServiceTests`): 6 passed, 0 failed

**Integration tests** (`Testurio.IntegrationTests/Controllers/StatsControllerTests`): 12 passed, 0 failed

### Frontend — 2026-05-16

**Component tests** (Jest):
- `TrendChart.test.tsx` — 4 passed
- `RunHistoryTable.test.tsx` — 4 passed
- `ScenarioCard.test.tsx` — 4 passed
- `RunDetailPanel.test.tsx` — 6 passed
- `ProjectHistoryPage.test.tsx` — 8 passed

**Total: 34 frontend tests passed, 0 failed**

### Bugs fixed during test phase

- `tests/Testurio.UnitTests/Services/ProjectHistoryServiceTests.cs:67` — invalid C# nullable tuple cast syntax `(Type)? default`; replaced with `default((Type)?)` 
- `tests/Testurio.IntegrationTests/Controllers/StatsControllerTests.cs:159` — same invalid cast syntax; same fix
- `tests/Testurio.UnitTests/Services/ProjectHistoryServiceTests.cs:164` — `with` expression used on a `class` (not a `record`); replaced with direct object initializer including explicit `Id = "result-1"`
- `src/views/ProjectHistoryPage/ProjectHistoryPage.test.tsx:193` — assertion `toHaveAttribute('data-run-id', '')` incorrect when React receives `null`; corrected to `not.toHaveAttribute('data-run-id')`
- `src/components/RunDetailPanel/RunDetailPanel.test.tsx:91` — queried `container` for MUI Skeleton elements, but MUI Drawer renders into a portal outside the container; corrected to `document.body.querySelectorAll`

### T030 E2E spec — created during test phase

`source/Testurio.Web/e2e/project-history.spec.ts` — 9 tests covering AC-001/002, AC-003/005, AC-007, AC-008, AC-009, AC-010/040, AC-011/012/014, AC-006/019/022, AC-026/028/031, AC-042/043, AC-046

---

## Amendments

_Populated when spec or plan changes after initial approval. Format:_

```
### Amendment — YYYY-MM-DD
**Changed**: [which documents were updated]
**Reason**: [why the change was needed]
**Impact**: [phases that need to re-run as a result]
```
