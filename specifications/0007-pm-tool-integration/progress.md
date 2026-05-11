# Progress — PM Tool Integration (0007)

## Phase Status

| Phase     | Status      | Date       | Notes |
| --------- | ----------- | ---------- | ----- |
| Specify   | ✅ Complete | 2026-05-09 |       |
| Plan      | ✅ Complete | 2026-05-09 |       |
| Implement | ✅ Complete | 2026-05-11 |       |
| Review    | ✅ Complete | 2026-05-11 |       |
| Test      | ⏳ Pending  |            |       |

---

## Implementation Notes

_Populated by `/implement [####]`_

---

## Review — 2026-05-11

### Warnings fixed
- `source/Testurio.Api/Services/PMToolConnectionService.cs`:74-79 — Validation errors returned as `IReadOnlyList<string>` lost field names from `ValidationResult.MemberNames`; changed to `IDictionary<string, string[]>` and added `ToErrorDictionary` helper that maps member names to keys
- `source/Testurio.Api/Endpoints/IntegrationEndpoints.cs`:79-82 — `ValidationProblem` errors were keyed by integer index (`"0"`, `"1"`) instead of field names; fixed by passing the `IDictionary` directly to `TypedResults.ValidationProblem`
- `source/Testurio.Web/src/views/IntegrationPage/IntegrationPage.tsx`:48 — Referenced `integration?.webhookSecretUri` which does not exist on `PMToolConnectionResponse` DTO (it is a server-side entity field never included in responses per AC-035); replaced with `isConfigured` which correctly gates the webhook setup panel

### Suggestions fixed
- `source/Testurio.Api/Services/IPMToolConnectionService.cs` — Interface was co-located in `PMToolConnectionService.cs`; extracted to its own file as specified in plan T008

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
