# Progress — Secrets Migration to Azure Key Vault (0048)

## Phase Status

| Phase     | Status      | Date       | Notes |
| --------- | ----------- | ---------- | ----- |
| Specify   | ✅ Complete | 2026-06-06 |       |
| Plan      | ✅ Complete | 2026-06-06 |       |
| Implement | ✅ Complete | 2026-06-06 |       |
| Review    | ✅ Complete | 2026-06-06 |       |
| Test      | ✅ Complete | 2026-06-06 |       |

---

## Implementation Notes

_Populated by `/implement 0048`_

---

## Review — 2026-06-06

### Warnings fixed
- `source/Testurio.Infrastructure/KeyVaultSecretResolver.cs` — `ISecretResolver` still constructed a second independent `SecretClient` in both `Program.cs` files instead of delegating to `IKeyVaultSecretLoader` as specified in T024/T027. Added a preferred constructor `KeyVaultSecretResolver(IKeyVaultSecretLoader, string)` that delegates `ResolveAsync` to the loader; updated both `Program.cs` files to use it.
- `source/Testurio.Worker/Program.cs` — `ISecretResolver` production registration did not delegate to `IKeyVaultSecretLoader`; fixed alongside the API Program.cs change above.

### Suggestions fixed
- `source/Testurio.Infrastructure/DependencyInjection.cs:334` — XML doc comment on `AddAzureOpenAI` still referenced `AzureOpenAI:ApiKey` in configuration even though it was moved to Key Vault-backed `AzureOpenAISecrets`; updated comment to reflect current design.
- `tests/Testurio.UnitTests/Infrastructure/KeyVaultSecretLoaderTests.cs:24-51` — `NullKeyVaultSecretLoader` tests were duplicated in `KeyVaultSecretLoaderTests`; removed the duplicates and cleaned up the now-unused `Moq` import.

### Status: Complete

---

## Test Results — 2026-06-06

**Unit Tests**: 564 passed, 0 failed
- `KeyVaultSecretLoaderTests` — argument validation tests fixed to accept ArgumentNullException/ArgumentException per ThrowIfNullOrWhiteSpace semantics using Assert.ThrowsAny
- `NullKeyVaultSecretLoaderTests` — verified no-op behaviour across all secret names
- `SecretsRegistrationTests` — verified dev/prod mode switching for all *Secrets classes

**Integration Tests**: 203 passed, 0 failed
- `ApiStartupSecretsTests` — API starts cleanly with NullKeyVaultSecretLoader, all secrets registered, IOptions<T> validation passes
- `WorkerStartupSecretsTests` — Worker starts cleanly with NullKeyVaultSecretLoader, all secrets registered

**Test Fixes Applied**:
1. `tests/Testurio.UnitTests/Infrastructure/KeyVaultSecretLoaderTests.cs` — Updated constructor and method argument validation assertions to use `Assert.ThrowsAny<ArgumentException>` to handle both ArgumentNullException (for null) and ArgumentException (for empty/whitespace)
2. `tests/Testurio.IntegrationTests/Startup/WorkerStartupSecretsTests.cs` — Set builder.Environment.EnvironmentName = "Development" in BuildWorkerHost to avoid KeyVault:Uri requirement during tests
3. `tests/Testurio.IntegrationTests/Startup/ApiStartupSecretsTests.cs` — Added builder.UseEnvironment("Development") in Factory.ConfigureWebHost to ensure tests run in Development mode

**Acceptance Criteria Validation**: All 59 acceptance criteria covered by passing tests

---

## Amendments

_Populated when spec or plan changes after initial approval. Format:_

```
### Amendment — YYYY-MM-DD
**Changed**: [which documents were updated]
**Reason**: [why the change was needed]
**Impact**: [phases that need to re-run as a result]
```
