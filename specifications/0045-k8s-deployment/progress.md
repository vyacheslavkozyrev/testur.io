# Progress — Self-Hosted Kubernetes Deployment (0045)

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

All 27 tasks completed on 2026-05-19. Created Dockerfiles for Api and Web services, full Kubernetes manifest set under `infra/k8s/base/` (namespace, RBAC, configmaps, secret templates, deployments, services), Kustomize base and staging/production overlays, k8s README, .gitignore update, and three GitHub Actions workflows (k8s-pr.yml, k8s-promote-staging.yml, k8s-promote-production.yml). Worker Dockerfile from feature 0044 was verified intact. Web's `next.config.ts` was updated to enable standalone output mode required by the distroless Dockerfile.

---

## Review — 2026-05-19

### Blockers fixed
- `source/Testurio.Api/Dockerfile`:1,16 — Base images were `sdk:9.0`/`aspnet:9.0`; AC-007 and AC-012 require `sdk:10.0`/`aspnet:10.0`. Updated both FROM lines to .NET 10.

### Status: Complete

---

## Test Results

### Test Run — 2026-05-19 (initial)

**Method**: Static manifest inspection (no runnable unit/integration tests exist for infrastructure files).

**Result: FAILED — 3 gaps found. 67 of 70 criteria pass.**

Gaps: AC-035 (README sequence), AC-048 (missing job-level `outputs:` block), AC-070 (KUBECONFIG scoped to service account not documented).

---

### Test Run — 2026-05-19 (re-run after gap fixes)

**Method**: Static manifest and live `kubectl kustomize` inspection.

**Result: FAILED — 1 gap found. 69 of 70 criteria pass.**

#### Previously Failing — Now Passing

**AC-035** — FIXED. `infra/k8s/README.md` now documents the correct pre-deployment sequence:
- Step 1: substitute and apply Secrets
- Step 2: apply ConfigMaps
- Step 3: apply Deployments and Services (`kubectl apply -k`)
- Step 4: create `regcred` image pull secret

**AC-048** — FIXED. The `build-and-push` job now contains a formal `outputs:` block:
```yaml
outputs:
  SHORT_SHA: ${{ steps.sha.outputs.SHORT_SHA }}
```
`SHORT_SHA` is computed in step `id: sha` and exposed as a job-level output consumable by downstream workflows via `needs.build-and-push.outputs.SHORT_SHA`.

**AC-070** — FIXED. `infra/k8s/README.md` now contains a dedicated "Configuring the `KUBECONFIG` GitHub Secret" section with a step-by-step script that creates a `kubernetes.io/service-account-token` Secret for `testurio-deployer`, extracts the token, and assembles a kubeconfig scoped to that service account (not cluster-admin). The section explicitly states: "The CI/CD workflows authenticate to the cluster as the `testurio-deployer` service account — never as cluster-admin."

#### Passing Criteria (69)

AC-001 through AC-034, AC-036 through AC-041, AC-043 through AC-070 — all pass except AC-042 (see gap below).

All Dockerfiles, Kubernetes manifests, secret templates, RBAC, Kustomize base, staging/production overlays, GitHub Actions workflows (k8s-pr.yml, k8s-promote-staging.yml, k8s-promote-production.yml), `.gitignore` pattern, and README are present and correctly structured.

#### Gap

**AC-042** — `kubectl kustomize infra/k8s/overlays/staging` fails at runtime.

- Command run: `kubectl kustomize infra/k8s/overlays/staging`
- Error: `trouble configuring builtin PatchTransformer ... unable to parse SM or JSON patch from [apiVersion: v1 kind: ConfigMap ...]`
- Root cause: `infra/k8s/overlays/staging/configmap-patch.yaml` (and the production equivalent) is a multi-document YAML file containing two ConfigMap documents separated by `---`. The `patches:` block in both overlay `kustomization.yaml` files references this file twice — once with `target: name: web-config` and once with `target: name: api-config`. Kustomize's strategic merge patch engine cannot parse a multi-document YAML file when used as a single patch path with an explicit `target:` override; it expects one document per patch file.
- Fix required: split `configmap-patch.yaml` into two separate files (e.g. `web-configmap-patch.yaml` and `api-configmap-patch.yaml`) in both the staging and production overlays, and update each `kustomization.yaml` to reference the correct split files.

---

### Test Run — 2026-05-19 (final, after AC-042 fix)

**Method**: Static manifest inspection + live `kubectl kustomize` execution on both staging and production overlays.

**Result: PASSED — all 70 of 70 criteria pass.**

#### AC-042 — FIXED and VERIFIED

- Cause: `configmap-patch.yaml` was a multi-document YAML file; Kustomize strategic-merge patch engine requires one document per patch file.
- Fix applied: split into `web-configmap-patch.yaml` and `api-configmap-patch.yaml` in both `overlays/staging/` and `overlays/production/`; each `kustomization.yaml` updated to reference the split files.
- Verified: `kubectl kustomize infra/k8s/overlays/staging` and `kubectl kustomize infra/k8s/overlays/production` both produce complete, valid manifest sets with no errors.

#### All Passing Criteria (70 / 70)

AC-001 through AC-070 — all pass. Dockerfiles (Web, API), Kubernetes manifests (base and overlays), Kustomize structure, secret templates, RBAC, README, `.gitignore` pattern, and all three GitHub Actions workflows are present and correctly structured.

---

## Amendments

_Populated when spec or plan changes after initial approval. Format:_

```
### Amendment — YYYY-MM-DD
**Changed**: [which documents were updated]
**Reason**: [why the change was needed]
**Impact**: [phases that need to re-run as a result]
```
