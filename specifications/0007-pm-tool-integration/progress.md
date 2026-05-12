# Progress — PM Tool Integration (0007)

## Phase Status

| Phase     | Status      | Date       | Notes |
| --------- | ----------- | ---------- | ----- |
| Specify   | ✅ Complete | 2026-05-09 |       |
| Plan      | ✅ Complete | 2026-05-09 |       |
| Implement | ✅ Complete | 2026-05-11 |       |
| Review    | ✅ Complete | 2026-05-11 |       |
| Test      | ✅ Complete | 2026-05-11 |       |

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

### 2026-05-11

**Backend unit tests (T027):** 16/16 passed — `PMToolConnectionServiceTests` covers all US story acceptance criteria including SaveADO/Jira, TestConnection (ok/auth_error/unreachable), RemoveConnection, webhook secret generation (plaintext first view, masked subsequent), integration status, and forbidden cross-tenant access.

**Backend integration tests (T028):** 12/12 passed — `PMToolIntegrationTests` covers POST/PUT ADO and Jira save (200 OK and 400 validation), GET integration status (200/401/403), DELETE integration (200/403), POST test-connection (200 structured result), GET webhook-setup (200).

**Frontend hook tests (T029):** 8/8 passed — `usePMToolConnection.test.ts` covers query invalidation on mutation, error state handling, loading states.

**Frontend component tests (T030–T031):** 21/21 passed — ADOConnectionForm (6), JiraConnectionForm (6), WebhookSetupPanel (7) covering field presence, validation display, submission, secret masking, copy-to-clipboard, and regenerate confirmation flow.

**Fixes applied during test phase:**
- Installed missing `@mui/icons-material@^6.5.0` dependency (required by `WebhookSetupPanel.tsx`)
- Added `@testing-library/dom` to `devDependencies` (peer dep of `@testing-library/react`)
- Fixed ambiguous `getByText(/In Testing/i)` query in `ADOConnectionForm.test.tsx` → `getByLabelText`
- Fixed ambiguous `getByLabelText(/Personal Access Token/i)` and `getByLabelText(/API Token/i)` queries in form tests → added `{ selector: 'input' }` option to resolve MUI Select option text conflicts

**All 57 tests pass. All acceptance criteria covered.**

---

## Amendments

_Populated when spec or plan changes after initial approval. Format:_

```
### Amendment — YYYY-MM-DD
**Changed**: [which documents were updated]
**Reason**: [why the change was needed]
**Impact**: [phases that need to re-run as a result]
```
