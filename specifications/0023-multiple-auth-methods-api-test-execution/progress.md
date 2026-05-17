# Progress — Multiple Authentication Methods for API Test Execution (0023)

## Phase Status

| Phase     | Status      | Date       | Notes |
| --------- | ----------- | ---------- | ----- |
| Specify   | ✅ Complete | 2026-05-15 |       |
| Plan      | ✅ Complete | 2026-05-15 |       |
| Implement | ✅ Complete | 2026-05-17 |       |
| Review    | ✅ Complete | 2026-05-17 |       |
| Test      | ✅ Complete | 2026-05-17 |       |

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

**Backend Unit Tests**: 305/305 PASSED ✓ (includes all 0023 tests)
- All ProjectApiAuthService tests: PASSED
- All ApiTestAuthCredentialProvider tests: PASSED
- All HttpExecutor API auth injection tests: PASSED

**Backend Integration Tests**: 11/11 PASSED ✓ (ProjectApiAuth suite)
- Root cause of earlier failures: `[AllowedValues("header", "query")]` on the nullable `ApiAuthApiKeyPlacement` property was rejecting `null` values (when method is "none", "bearer", or "basic"), causing the ValidationFilter to return 400 for all non-api_key PATCH requests. Fix: removed the attribute and moved the check into `IValidatableObject.Validate()`.
- All GET and PATCH tests now pass.

**Frontend Component Tests**: 21/21 PASSED ✓
- Root cause of earlier failures: MUI `required` TextField renders label as "Token *" (with aria-hidden asterisk span), causing `getByLabelText('Token')` exact-string match to fail. Fix: changed to anchored regex `/^Token/i`.
- Additionally refactored validation error tests to use a `trySave()` helper that wraps `save()` inside `act()` without relying on `.rejects.toThrow()` (which does not guarantee React state flush before subsequent assertions).

### Status

Test phase COMPLETE. All tests pass.

---

## Amendments

_Populated when spec or plan changes after initial approval. Format:_

```
### Amendment — YYYY-MM-DD
**Changed**: [which documents were updated]
**Reason**: [why the change was needed]
**Impact**: [phases that need to re-run as a result]
```
