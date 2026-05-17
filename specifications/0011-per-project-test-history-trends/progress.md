# Progress — Per-Project Test History & Trends (0011)

## Phase Status

| Phase     | Status      | Date       | Notes |
| --------- | ----------- | ---------- | ----- |
| Specify   | ✅ Complete | 2026-05-16 |       |
| Plan      | ✅ Complete | 2026-05-16 |       |
| Implement | ✅ Complete | 2026-05-16 |       |
| Review    | ✅ Complete | 2026-05-16 |       |
| Test      | ⏳ Pending  |            |       |

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

_Populated by `/test 0011`_

---

## Amendments

_Populated when spec or plan changes after initial approval. Format:_

```
### Amendment — YYYY-MM-DD
**Changed**: [which documents were updated]
**Reason**: [why the change was needed]
**Impact**: [phases that need to re-run as a result]
```
