# Progress — Secrets Migration to Azure Key Vault (0048)

## Phase Status

| Phase     | Status      | Date       | Notes |
| --------- | ----------- | ---------- | ----- |
| Specify   | ✅ Complete | 2026-06-06 |       |
| Plan      | ✅ Complete | 2026-06-06 |       |
| Implement | ✅ Complete | 2026-06-06 |       |
| Review    | ✅ Complete | 2026-06-06 |       |
| Test      | ⏳ Pending  |            |       |

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

## Test Results

_Populated by `/test 0048`_

---

## Amendments

_Populated when spec or plan changes after initial approval. Format:_

```
### Amendment — YYYY-MM-DD
**Changed**: [which documents were updated]
**Reason**: [why the change was needed]
**Impact**: [phases that need to re-run as a result]
```
