# Progress — Testing Environment Access Configuration (0017)

## Phase Status

| Phase     | Status      | Date       | Notes |
| --------- | ----------- | ---------- | ----- |
| Specify   | ✅ Complete | 2026-05-15 |       |
| Plan      | ✅ Complete | 2026-05-15 |       |
| Implement | ✅ Complete | 2026-05-16 |       |
| Review    | ✅ Complete | 2026-05-16 |       |
| Test      | ⏳ Pending  |            |       |

---

## Implementation Notes

_Populated by `/implement 0017`_

---

## Review — 2026-05-16

### Blockers fixed
- `source/Testurio.Api/Services/ProjectAccessService.cs`:70-120 — **AC-040 atomicity violation**: old secrets were cleared before new secrets were written to Key Vault. If a new StoreAsync threw, old credentials were already wiped but Cosmos was not yet updated, leaving the project unconfigurable. Fixed by capturing old URIs upfront, writing new secrets first, updating Cosmos second, and only then clearing old secrets.

### Warnings fixed
- `source/Testurio.Web/src/components/AccessModeSelector/AccessModeSelector.tsx`:41-47 — **setState called during render**: `useMemo(() => ({ current: false }))` was used as a ref and setter functions were called directly in the render body. Violates React's rules of rendering and causes extra re-renders. Fixed by replacing with `useRef(false)` + `useEffect` (matching the established WorkItemTypeFilter pattern).

### Suggestions fixed
- `source/Testurio.Web/src/components/AccessModeSelector/AccessModeSelector.tsx`:215 — **Redundant empty FormHelperText** between Header Name and Header Value TextFields. The TextField already renders its own helperText; the empty element added no value. Removed.
- `source/Testurio.Core/Interfaces/IProjectAccessCredentialProvider.cs`:8-13 — **AC-032 ownership documentation gap**: interface did not explain where userId scoping is enforced. Added remarks documenting that API-layer validation is in ProjectAccessService and pipeline-layer isolation is enforced by the Cosmos DB partition key.
- `tests/Testurio.UnitTests/Services/ProjectAccessServiceTests.cs` — **Missing AC-040 regression test**: no test verified Cosmos is untouched when a Key Vault write fails. Added `UpdateAsync_DoesNotUpdateCosmos_WhenKeyVaultWriteFails_AC040`.

### Remaining issues
None.

### Status: Complete

---

## Test Results

_Populated by `/test 0017`_

---

## Amendments

_Populated when spec or plan changes after initial approval. Format:_

```
### Amendment — YYYY-MM-DD
**Changed**: [which documents were updated]
**Reason**: [why the change was needed]
**Impact**: [phases that need to re-run as a result]
```
