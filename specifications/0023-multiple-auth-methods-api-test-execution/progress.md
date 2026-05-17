# Progress — Multiple Authentication Methods for API Test Execution (0023)

## Phase Status

| Phase     | Status      | Date       | Notes |
| --------- | ----------- | ---------- | ----- |
| Specify   | ✅ Complete | 2026-05-15 |       |
| Plan      | ✅ Complete | 2026-05-15 |       |
| Implement | ✅ Complete | 2026-05-17 |       |
| Review    | ✅ Complete | 2026-05-17 |       |
| Test      | ⏳ Pending  |            |       |

---

## Implementation Notes

_Populated by `/implement 0023`_

---

## Review — 2026-05-17

### Blockers fixed
- `source/Testurio.Infrastructure/KeyVault/ApiTestAuthCredentialProvider.cs` — Added empty-value guards after resolving each secret (bearer token, API key value, basic password); empty resolved value now throws `CredentialRetrievalException` instead of silently producing a broken auth header. Also replaced `ex.Message` in the catch block with a generic phrase to prevent potential Key Vault secret names from leaking into error messages.
- `source/Testurio.Web/src/components/ApiAuthMethodSelector/ApiAuthMethodSelector.tsx` — Replaced `sameMethod` bypass in `validate()` with `existingBearer`/`existingApiKeyValue`/`existingBasicPassword` checks derived from `auth`. Secret fields are now optional only when a stored value is confirmed by the server; empty fields without a stored credential correctly fail validation.

### Warnings fixed
- `source/Testurio.Core/Interfaces/ISecretResolver.cs` — Added XML doc comments documenting that `StoreAsync(name, string.Empty)` is the accepted soft-delete/revocation pattern and that callers must validate non-empty resolved values.
- `source/Testurio.Api/Services/ProjectService.cs` — Replaced fragile `ToString().ToLowerInvariant()` switch (which produced `"apikey"` for `ApiKey`) with a direct enum switch producing the correct `"api_key"` string.
- `tests/Testurio.UnitTests/Services/ProjectApiAuthServiceTests.cs` — Added `UpdateAsync_SwitchingFromApiKeyToNone_ClearsApiKeyValueSecret`, `UpdateAsync_SwitchingFromBasicToNone_ClearsBasicPasswordSecret`, and `UpdateAsync_BearerToBearerWithFailingStore_DoesNotUpdateCosmos_AndDoesNotAttemptCleanup` tests.
- `tests/Testurio.UnitTests/Services/ApiTestAuthCredentialProviderTests.cs` — Added `ResolveAsync_ThrowsCredentialRetrievalException_WhenBearerTokenResolvedEmpty` test covering the new empty-value guard.
- `source/Testurio.Web/src/components/ApiAuthMethodSelector/ApiAuthMethodSelector.test.tsx` — Added test asserting that `bearer` method loaded with `apiAuthBearerTokenConfigured: false` and no typed token fails validation with "Token is required."

### Suggestions fixed
- `source/Testurio.Web/src/components/ApiAuthMethodSelector/ApiAuthMethodSelector.tsx` — Moved `useMemo` out of the module-level `getStyles` function and into the component call site (`useMemo(() => getStyles(theme), [theme])`); removed the `// eslint-disable-next-line react-hooks/rules-of-hooks` comment.
- `tests/Testurio.UnitTests/Pipeline/Executors/HttpExecutorTests.cs` — Removed duplicate `ApplyApiAuthCredentials_None_DoesNotSetAuthorizationHeader` test; strengthened `ApplyApiAuthCredentials_None_AddsNoAuthHeader` with `Assert.False(Contains("Authorization"))`.

### Status: Complete

---

## Test Results

### 2026-05-17

**Backend Unit Tests**: 38/38 PASSED ✓
- All ProjectApiAuthService tests: PASSED
- All ApiTestAuthCredentialProvider tests: PASSED  
- All HttpExecutor API auth injection tests: PASSED

**Backend Integration Tests**: 7/11 PASSED, 4 FAILED ⚠️
- FAILED: PatchProjectApiAuth_Returns200_WithBearerTokenConfiguredTrue
- FAILED: PatchProjectApiAuth_Returns200_WithNoneMethod
- FAILED: PatchProjectApiAuth_Returns403_WhenProjectBelongsToDifferentUser  
- FAILED: PatchProjectApiAuth_Returns404_WhenProjectNotFound
- All failing tests are PATCH requests receiving 400 Bad Request instead of expected status
- PASSED: All GET requests and PATCH requests with intentional validation errors (expecting 400)

**Frontend Component Tests**: 13/21 PASSED, 8 FAILED ⚠️
- Multiple test failures with "Found multiple elements with the text" for Token label
- Suggests component rendering issue rather than logic issue

### Status

Test phase INCOMPLETE. Unit tests pass (business logic verified), but integration and frontend tests have failures blocking completion.

---

## Amendments

_Populated when spec or plan changes after initial approval. Format:_

```
### Amendment — YYYY-MM-DD
**Changed**: [which documents were updated]
**Reason**: [why the change was needed]
**Impact**: [phases that need to re-run as a result]
```
