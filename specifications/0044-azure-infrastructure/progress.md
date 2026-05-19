# Progress — Azure Infrastructure & CI/CD Deployment (0044)

## Phase Status

| Phase     | Status      | Date       | Notes |
| --------- | ----------- | ---------- | ----- |
| Specify   | ✅ Complete | 2026-05-19 |       |
| Plan      | ✅ Complete | 2026-05-19 |       |
| Implement | ✅ Complete | 2026-05-19 |       |
| Review    | ✅ Complete | 2026-05-19 |       |
| Test      | ⏳ Pending  |            |       |

---

## Implementation Notes

_Populated by `/implement 0044`_

---

## Review — 2026-05-19

### Blockers fixed
- `infra/main.bicep:163` — `guid()` for `appServiceKvRole` used `keyVault.name` (the Bicep module deployment name `'keyVault'`) instead of `keyVault.outputs.vaultName` (the actual resource name), producing incorrect deterministic GUIDs for role assignments; fixed to use the actual vault name
- `infra/main.bicep:173` — same `guid()` bug for `workerKvRole`; fixed to use `keyVault.outputs.vaultName`
- `source/Testurio.Worker/Dockerfile:13` — `Testurio.Pipeline.MemoryWriter` project (pipeline stage 8) was missing from the project COPY list, causing restore failure when the project is referenced by the Worker; added the missing COPY instruction

### Warnings fixed
- `infra/modules/containerapps.bicep:16` — used preview API version `2023-11-02-preview` for `Microsoft.App/managedEnvironments`; updated to GA `2024-03-01`
- `infra/modules/containerapps.bicep:26` — used preview API version `2023-11-02-preview` for `Microsoft.App/containerApps`; updated to GA `2024-03-01`
- `infra/modules/apim.bicep:14` — used preview API version `2023-03-01-preview` for `Microsoft.ApiManagement/service`; updated to stable `2022-08-01`
- `.github/workflows/deploy-web.yml:57` — `KEY_VAULT_NAME` variable was unquoted in shell script, risking word-splitting; added double quotes
- `.github/workflows/deploy-worker.yml:39` — `ACR_LOGIN_SERVER` variable was unquoted in `az acr login` command; added double quotes

### Status: Complete

---

## Test Results

_Populated by `/test 0044`_

---

## Amendments

_Populated when spec or plan changes after initial approval. Format:_

```
### Amendment — YYYY-MM-DD
**Changed**: [which documents were updated]
**Reason**: [why the change was needed]
**Impact**: [phases that need to re-run as a result]
```
