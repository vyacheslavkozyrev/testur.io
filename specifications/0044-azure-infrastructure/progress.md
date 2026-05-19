# Progress — Azure Infrastructure & CI/CD Deployment (0044)

## Phase Status

| Phase     | Status      | Date       | Notes |
| --------- | ----------- | ---------- | ----- |
| Specify   | ✅ Complete | 2026-05-19 |       |
| Plan      | ✅ Complete | 2026-05-19 |       |
| Implement | ✅ Complete | 2026-05-19 |       |
| Review    | ✅ Complete | 2026-05-19 |       |
| Test      | ✅ Complete | 2026-05-19 |       |

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

## Test Results — 2026-05-19 (initial run)

### Status: FAILED — 6 gaps, no passing automated tests

No automated test suite exists for this infrastructure feature (expected per plan rationale). Validation was performed by static inspection of all authored files against each acceptance criterion in `stories.md`.

#### Passing criteria (45 / 61)

AC-001 through AC-007, AC-010 through AC-013, AC-015, AC-016, AC-018 through AC-041, AC-043 through AC-054, AC-056 through AC-061 all pass on static inspection.

#### Gaps

- **AC-008 / AC-009** — `main.bicep` does not wire `cosmos.bicep` or `servicebus.bicep` as module declarations. The environment-driven Cosmos DB serverless (dev) / provisioned 400 RU/s (prod) and Service Bus Standard (dev) / Premium (prod) SKU distinctions required by these criteria are therefore not enforced. Both module files exist but are absent from the root orchestrator.

- **AC-014** — Secret placeholder names seeded by `bootstrap.sh` do not match the names specified. Required: `stripe-secret-key`, `stripe-webhook-secret`, `anthropic-api-key`, `cosmos-connection-string`, `servicebus-connection-string`, `adb2c-client-secret`. Actual seeded: `servicebus-connection`, `cosmos-connection`, `anthropic-api-key`, `swa-deployment-token`. Missing: `stripe-secret-key`, `stripe-webhook-secret`, `adb2c-client-secret`. Name mismatches: `cosmos-connection` vs `cosmos-connection-string`, `servicebus-connection` vs `servicebus-connection-string`.

- **AC-017** — App Service settings use `AZURE_KEY_VAULT_URI` for programmatic lookup rather than the native App Service Key Vault reference syntax (`@Microsoft.KeyVault(SecretUri=...)`). The AC explicitly requires the `@Microsoft.KeyVault(...)` reference syntax in app settings so secrets never leave Key Vault.

- **AC-042** — `bootstrap.sh` outputs `WORKER_APP_NAME_DEV` / `WORKER_APP_NAME_PROD` but the acceptance criterion specifies `CONTAINER_APP_NAME_DEV` / `CONTAINER_APP_NAME_PROD`.

- **AC-055** — `aisearch.bicep` references the vector index only in a comment. The Bicep resource definition for the Search service contains no ARM-level vector index schema (algorithm configuration, field mapping, 1536 dimensions, cosine distance). The index must be defined either as a nested resource or a separate `Microsoft.Search/searchServices/indexes` resource in the module.

---

## Test Results — 2026-05-19 (re-run after gap fixes)

### Status: PASSED — 61 / 61 acceptance criteria

No automated test suite exists for this infrastructure feature (expected per plan rationale). Validation was performed by static inspection of all authored files against each acceptance criterion in `stories.md`.

All 6 previously failing gaps are confirmed fixed:

- **AC-008 / AC-009** — `infra/main.bicep` now wires both `modules/cosmos.bicep` (with `serverless: !isProd` driving dev=serverless / prod=provisioned 400 RU/s) and `modules/servicebus.bicep` (with `skuName: isProd ? 'Premium' : 'Standard'`). Environment-driven SKU logic confirmed.

- **AC-014** — `infra/bootstrap.sh` SECRETS array now contains all 7 correct names: `stripe-secret-key`, `stripe-webhook-secret`, `anthropic-api-key`, `cosmos-connection-string`, `servicebus-connection-string`, `adb2c-client-secret`, `swa-deployment-token`. All required names match spec.

- **AC-017** — `infra/modules/appservice.bicep` app settings now use `@Microsoft.KeyVault(VaultName=...;SecretName=...)` reference syntax for `Stripe__SecretKey`, `Stripe__WebhookSecret`, `Cosmos__ConnectionString`, `ServiceBus__ConnectionString`, and `AzureAdB2C__ClientSecret`. No secret values appear in app settings.

- **AC-042** — `.github/workflows/deploy-worker.yml` uses `${{ vars.CONTAINER_APP_NAME }}` and `infra/bootstrap.sh` outputs `CONTAINER_APP_NAME_${ENV_UPPER}`. Both match the spec variable name.

- **AC-055** — `infra/modules/aisearch.bicep` defines a full `Microsoft.Search/searchServices/indexes` nested resource (`test-memory`) with HNSW algorithm (cosine distance, m=4, efConstruction=400), 1536-dimension `storyEmbedding` field, vector search profile, and filter fields for `userId`, `testType`, and `isDeleted`. Matches spec exactly.

- **Container Apps KV URIs** — `infra/main.bicep` passes `'${keyVault.outputs.vaultUri}secrets/servicebus-connection-string'` and `'${keyVault.outputs.vaultUri}secrets/cosmos-connection-string'` to `containerapps.bicep`, aligning with the corrected secret names.

#### All 61 criteria pass on static inspection.

---

## Amendments

_Populated when spec or plan changes after initial approval. Format:_

```
### Amendment — YYYY-MM-DD
**Changed**: [which documents were updated]
**Reason**: [why the change was needed]
**Impact**: [phases that need to re-run as a result]
```
