# CI/CD State — June 2026

> Snapshot of current pipeline implementation, known issues, and tech debt.
> Last updated: June 2026

---

## Current Implementation

### Components in scope

| Component         | Language             | Status               |
| ----------------- | -------------------- | -------------------- |
| `Testurio.Web`    | Next.js 15 / Node 24 | ✅ Pipeline complete |
| `Testurio.Api`    | ASP.NET Core 9       | ✅ Pipeline complete |
| `Testurio.Worker` | ASP.NET Core 9       | 🔲 Not started       |

---

## Pipeline Flow

### PR Open → develop

```
feature/*
    │
    │  open PR → develop
    ▼
─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─
 PR VALIDATION WORKFLOW
   UI:  install → lint → test → audit
   API: build → lint (format check) → audit
─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─
```

### Merge to develop → Tag → Deploy

```
    │  merge to develop
    ▼
─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─
 RELEASE-PLEASE (watches develop)
   reads conventional commits since last tag
   opens Release PR (bumps version + CHANGELOG)
   auto-merges Release PR (Phase 1)
   creates component-scoped git tag:
     testurio-web@1.3.0
     testurio-api@1.3.0
─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─
    │
    │  on: push tag testurio-web@* / testurio-api@*
    ▼
─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─
 DEPLOY WORKFLOW
   build binary (npm run build / dotnet publish)
   docker build → push to GHCR (tagged: 1.3.0)
        ↓
   trivy scan (HIGH/CRITICAL, ignore-unfixed)
        ↓
   cosign sign (keyless OIDC)
   syft SBOM → cosign attest
        ↓
   helm lint
        ↓
   helm package → push to GHCR OCI
   (chart: ghcr.io/org/repo/component/charts)
        ↓
   update GitOps repo (northLn/k3s-local)
   open + merge PR into feature/argocd
        ↓
   ArgoCD detects change → syncs dev
─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─
```

### Environments

| Environment | Branch    | Trigger                              | Promotion                 |
| ----------- | --------- | ------------------------------------ | ------------------------- |
| dev         | `develop` | tag push (release-please auto-merge) | automatic                 |
| stg         | —         | —                                    | Phase 2 (not implemented) |
| prd         | —         | —                                    | Phase 3 (not implemented) |

---

## Repository Structure

```
testur.io/                          ← monorepo
├── .github/
│   ├── workflows/
│   │   ├── ui-pr-validation.yml    ← PR checks for Web
│   │   ├── ui-deploy.yml           ← Deploy pipeline for Web
│   │   ├── api-pr-validation.yml   ← PR checks for API
│   │   ├── api-deploy.yml          ← Deploy pipeline for API
│   │   └── release-please.yml      ← Versioning for all components
│   ├── release-please-config.json  ← Component definitions
│   ├── .release-please-manifest.json ← Current versions
│   └── dependabot.yml              ← Automated dependency updates
├── source/
│   ├── Testurio.Web/
│   │   ├── Dockerfile              ← Full multi-stage (local dev)
│   │   ├── Dockerfile.ci           ← Runtime only (CI/CD)
│   │   └── chart/                  ← Helm chart
│   │       ├── Chart.yaml
│   │       └── values.yaml
│   ├── Testurio.Api/
│   │   ├── Dockerfile              ← Full multi-stage (local dev)
│   │   ├── Dockerfile.ci           ← Runtime only (CI/CD)
│   │   └── chart/                  ← Helm chart
│   │       ├── Chart.yaml
│   │       └── values.yaml
│   └── Testurio.Worker/            ← Pipeline not yet implemented
```

## GHCR Package Structure

```
ghcr.io/vyacheslavkozyrev/
└── testur.io/
    ├── testurio-web                ← Docker image
    ├── testurio-web/charts         ← Helm chart (OCI)
    ├── testurio-api                ← Docker image
    └── testurio-api/charts         ← Helm chart (OCI)
```

## GitOps Repo Structure (northLn/k3s-local)

```
argocd/clusters/dev01/workload/
├── testurio-web/
│   └── service.yaml               ← ArgoCD app definition (chartVersion updated by CI)
└── testurio-api/
    └── service.yaml               ← ArgoCD app definition (chartVersion updated by CI)
```

---

## Base Chart

- **Repo:** `northLn/charts` (separate private repo)
- **Registry:** `oci://ghcr.io/northln/charts`
- **Current version:** `0.1.1`
- **Features:** Deployment, Service, HTTPRoute (Gateway API), ServiceAccount, HPA, PDB, security contexts, probes, extraEnv, extraEnvFrom

---

## Infrastructure

| Component       | Details                                    |
| --------------- | ------------------------------------------ |
| Kubernetes      | k3s v1.35.4 on FCOS 43 (bare metal)        |
| CNI             | Cilium 1.19.3                              |
| Ingress         | Gateway API (Cilium)                       |
| GitOps          | ArgoCD (latest)                            |
| Registry        | GHCR (GitHub Container Registry)           |
| Node IP         | `10.0.22.10` (VLAN 22)                     |
| DNS             | Cloudflare (proxied)                       |
| External access | Cloudflare → Dream Router → Cilium Gateway |

---

## Tech Debt

### High Priority

| Item                           | Component   | Description                                                                                                                                                                                                                  |
| ------------------------------ | ----------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Node.js 20 → 24                | Web         | Actions deprecated as of June 2026. Migrate `node-version: 24` already done. Verify no runtime issues.                                                                                                                       |
| .NET 9 → 10                    | API, Worker | .NET 9 EOL November 2026. Migrate `TargetFramework` + package versions + base images.                                                                                                                                        |
| `NEXT_PUBLIC_*` baked at build | Web         | B2C config and API URL are compiled into the bundle — one image per environment. Implement runtime config injection via `/api/config` endpoint to enable build-once promote pattern.                                         |
| Key Vault authentication       | API         | Currently requires Azure Service Principal credentials (`AZURE_CLIENT_ID/SECRET/TENANT_ID`) in Kubernetes secret. Migrate to Azure Workload Identity for keyless auth when moving to AKS or installing OIDC provider on k3s. |
| Health endpoints               | API, Worker | No `/health` or `/ready` endpoints implemented. Probes disabled in Helm charts. Add `app.MapHealthChecks("/health")` and enable probes.                                                                                      |

### Medium Priority

| Item                                  | Component        | Description                                                                                                                    |
| ------------------------------------- | ---------------- | ------------------------------------------------------------------------------------------------------------------------------ | --- | ------------------------------------------------------ |
| `actions/checkout@v6` SHA             | All workflows    | Wrong SHA in several workflow files — v6 doesn't exist, should be v4.2.x. Dependabot will fix on next run.                     |
| `azure/setup-helm@v5`                 | Deploy workflows | Doesn't exist, use `@v4`. Dependabot will fix.                                                                                 |
| `codeql-action/upload-sarif` sub-path | UI, API deploy   | Dependabot doesn't track sub-action paths — update `github/codeql-action/upload-sarif` SHA manually when new versions release. |
| Release-please auto-merge             | All              | Phase 1: Release PRs auto-merge immediately. Phase 2/3: switch to manual merge gate before promoting to stg/prd.               |
| Worker pipeline                       | Worker           | `Testurio.Worker` has no CI/CD pipeline. Implement after API is stable.                                                        |
| Helm chart for Worker                 | Worker           | No Helm chart created yet.                                                                                                     |
| `dotnet format` enforcement           | API              | Lint step is non-blocking (`                                                                                                   |     | true`). Enable enforcement once codebase is formatted. |
| `dotnet list --vulnerable` exit code  | API              | Audit step reports but does not fail on vulnerabilities. Enable failure gate when team is ready.                               |

### Low Priority

| Item                               | Component | Description                                                                                                                                                                    |
| ---------------------------------- | --------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| Trivy binary version               | All       | `version: v0.70.0` in setup-trivy is manually pinned. Check for updates periodically — Dependabot does not manage this value.                                                  |
| SBOM format                        | All       | Currently `spdx-json`. Consider also generating `cyclonedx` for broader tooling compatibility.                                                                                 |
| GitHub Advanced Security           | All       | SARIF upload to GitHub Security tab commented out — requires paid Advanced Security. Uncomment when enabled.                                                                   |
| Cosign/SBOM in PR validation       | All       | Currently only in deploy pipeline. Consider adding image signing to PR builds for earlier validation.                                                                          |
| Multi-platform Docker images       | All       | Images built for `linux/amd64` only. Add `linux/arm64` for Apple Silicon dev machines and ARM k3s nodes. Add QEMU + `platforms: linux/amd64,linux/arm64` to build-push-action. |
| `Wired connection 1` NM connection | FCOS      | Old network connection kept as fallback after VLAN migration. Delete after confirming VLAN 22 is stable.                                                                       |

### Phase 2 (ArgoCD Image Updater)

| Item                        | Description                                                                                                                 |
| --------------------------- | --------------------------------------------------------------------------------------------------------------------------- |
| Replace GitOps PR flow      | ArgoCD Image Updater watches GHCR by digest, auto-updates GitOps repo. Eliminates `update-gitops` job from deploy pipeline. |
| Multi-environment promotion | Add stg environment with auto-promotion from dev, manual gate for prd.                                                      |
| Remove env-prefixed tags    | Image Updater tracks by digest — `dev-1.3.0` / `stg-1.3.0` tags no longer needed.                                           |

### Phase 3 (Kargo)

| Item                    | Description                                                                                              |
| ----------------------- | -------------------------------------------------------------------------------------------------------- |
| Full promotion pipeline | Kargo handles dev→stg→prd promotion with gates, approvals, and rollback. Replaces Phase 2 Image Updater. |
| Release-please on main  | Move release-please target branch from `develop` to `main` for proper release gating.                    |

---

## Conventional Commits Reference

| Prefix                         | Version bump | Example                          |
| ------------------------------ | ------------ | -------------------------------- |
| `fix:`                         | patch        | `fix: correct pagination offset` |
| `feat:`                        | minor        | `feat: add project export`       |
| `feat!:` or `BREAKING CHANGE:` | major        | `feat!: remove v1 API endpoints` |
| `chore:`, `docs:`, `ci:`       | none         | `chore: update dependencies`     |
