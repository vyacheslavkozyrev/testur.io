# CI/CD Workflow — Monorepo Components

## Overview

Every component that runs in production is packaged as a container image.
The image is the deployable unit, regardless of language or framework.

The monorepo currently contains **Node.js** and **.NET** components.
Each component owns its `Dockerfile.ci`, Helm chart, and workflow — all co-located with its source code.

Versioning is driven by **release-please** (Google, monorepo-native), which reads conventional commits
and manages semver automatically. Each component is versioned independently.

```
PR Open      →  lint → test
PR Merge     →  release-please opens Release PR (bumps package.json + CHANGELOG) → auto-merges → creates git tag
On git tag   →  build binary → build & push Docker image → package & push Helm chart → update GitOps repo → ArgoCD syncs
```

> Phase 1 covers the `develop` branch and `dev` environment only.
> Branches, tagging strategy, and promotion gates will expand in Phase 2 (ArgoCD Image Updater) and Phase 3 (Kargo).

---

## PR Open (`pull_request: opened, synchronize, reopened → develop`)

Triggered on every push to an open PR targeting `develop`.
Each component has its own workflow, scoped to its subdirectory via `paths:` filters.
**No binary is built and no image is produced at this stage.**

### 1. Install & Cache Dependencies
- Restore dependency cache keyed on the component's lockfile hash
- Run a clean, reproducible install:
  - Node.js: `npm ci` keyed on `package-lock.json`
  - .NET: `dotnet restore` keyed on `*.csproj` / `packages.lock.json`
- Cache resolved dependencies for downstream jobs in the same run

### 2. Lint
- Restore dependencies from cache
- Run the component's linter:
  - Node.js: `npm run lint`
  - .NET: `dotnet format --verify-no-changes`
- Fails fast — no point testing if code doesn't pass lint
- Runs in parallel with tests

### 3. Test
- Restore dependencies from cache
- Run the component's unit/integration test suite:
  - Node.js: `npm test`
  - .NET: `dotnet test`
- Runs in parallel with lint

> **This is the validation gate.** Both lint and test must pass before a PR can be merged.

---

## PR Merge (`push` to `develop`)

No code is built on merge. release-please runs and manages the release lifecycle.

### 1. release-please
- Reads conventional commits since the last tag for the component that changed
- Opens or updates a Release PR for that component:
  - Bumps `version` in `package.json` (Node.js) or `.csproj` (.NET)
  - Updates `CHANGELOG.md`
- Auto-merges the Release PR immediately (Phase 1)
- Creates a component-scoped git tag, e.g. `testurio-web@1.3.0` or `testurio-api@2.1.0`
- Only the component whose files changed gets a new tag — other components are unaffected

> Conventional commit types that drive version bumps:
> `fix:` → patch, `feat:` → minor, `feat!:` or `BREAKING CHANGE:` → major.
> `chore:`, `docs:`, `ci:` do not trigger a release.

> Auto-merge can be switched to manual PR review when promoting to higher environments in Phase 2/3.

---

## On Git Tag (`push` of tag matching `<component>@*`)

The tag event is the single trigger for all build and deploy activity.
The source code at the tag is identical to what was validated in the PR — the build is deterministic.

### 1. Build Binary
- Check out the repo at the tagged commit
- Install dependencies (warm cache unlikely on tag runners — full install)
- Compile or bundle the component:
  - Node.js: `npm run build` → outputs to `.next/standalone/` (Next.js standalone mode)
  - .NET: `dotnet publish` → outputs to `publish/`

### 2. Build & Push Docker Image
- Build the container image using `Dockerfile.ci` (runtime-only, no build stage)
- Copy only the built output into the image — no source code, no build tools, no dev dependencies
- Use a minimal base image appropriate for the runtime (see [Base Image Selection](#base-image-selection))
- Push to GHCR tagged as the bare semver from the git tag (e.g. `1.3.0`)
- Requires `permissions: packages: write` on the job

### 3. Package & Push Helm Chart
- The Helm chart lives next to the component's source code and `Dockerfile.ci`
- Update `Chart.yaml`:
  - `appVersion` → semver from the git tag (e.g. `1.3.0`)
  - `version` → same semver by default; can be bumped independently when chart structure changes
- Package the chart and push to GHCR as an OCI artifact:
  ```
  helm package .
  helm push <chart>-1.3.0.tgz oci://ghcr.io/<org>/<repo>/charts
  ```
- Chart and image live in the same GHCR registry — no separate chart repository needed

### 4. Sign Image _(optional)_
- Sign the pushed image using `sigstore/cosign-installer` + `cosign sign`
- Signature stored in GHCR alongside the image — no separate registry needed
- Keyless signing via GitHub OIDC — no long-lived secrets required
- Recommended for production workloads and compliance-sensitive environments

### 5. Generate & Attach SBOM _(optional)_
- Generate a Software Bill of Materials using `syft` or `docker buildx`
- Attach the SBOM to the image manifest in GHCR
- Recommended when compliance frameworks apply (SLSA, SOC2, FedRAMP)

### 6. Update GitOps Repo
- Check out the GitOps repo (contains ArgoCD `Application` manifests and environment values)
- Update `values-dev.yaml` for the component:
  - `image.tag: 1.3.0`
  - `chart version: 1.3.0`
- Commit and open a PR in the GitOps repo
- Auto-merge for `dev` (Phase 1) — ArgoCD detects the change and syncs

> **ArgoCD is never pushed to directly.** CI proposes the change via PR; ArgoCD pulls it. This is the core GitOps principle.

---

## Image Tagging Strategy

| Event | Tag Format | Example | Purpose |
|---|---|---|---|
| Git tag created | `<semver>` | `1.3.0` | Immutable, ties image to exact release |

All tags are immutable — no tag is ever overwritten. `latest` is never used.

**Phase 2/3 note:** ArgoCD Image Updater and Kargo track images by **digest**, not by tag.
Environment-prefixed tags (e.g. `dev-1.3.0`, `stg-1.3.0`) are therefore not needed and are intentionally omitted.
The plain semver tag is sufficient for all phases.

---

## Helm Chart Versioning

| Field | Value | Example | Notes |
|---|---|---|---|
| `appVersion` | semver from git tag | `1.3.0` | Tracks the app version |
| `version` | same as `appVersion` by default | `1.3.0` | Can be bumped independently when chart templates or structure change |
| `image.tag` (values) | semver | `1.3.0` | Same value across all environments; env distinction handled by ArgoCD values overlay |

---

## Versioning

Semver is sourced exclusively from **release-please** git tags. No manual version bumps in `package.json` or `.csproj` — release-please owns those files.

Tag format per component (release-please monorepo convention):
- `testurio-web@1.3.0` — Node.js UI
- `testurio-api@2.1.0` — .NET API

The deploy workflow strips the component prefix to extract the bare semver for image and chart tagging.

release-please config lives at the repo root in two files:
- `release-please-config.json` — component definitions, release types, options
- `.release-please-manifest.json` — current version per component (source of truth for next bump)

---

## Monorepo Notes

- Each component has its own workflow files, scoped via `paths:` filters to its subdirectory
- A top-level `env.WORKING_DIR` in each workflow points to the component's root
- `defaults.run.working-directory` is set per job — no `cd` repetition
- Dependency cache keys are scoped to the component's own lockfile — components never share or invalidate each other's caches
- The Docker build context and Helm chart are co-located in `WORKING_DIR`
- release-please `separate-pull-requests: true` ensures each component gets its own Release PR

---

## Dockerfile Strategy

Each component maintains two Dockerfiles:

| File | Purpose | Used by |
|---|---|---|
| `Dockerfile` | Full multi-stage build (build + runtime) | Local development, manual builds |
| `Dockerfile.ci` | Runtime only — expects pre-built output | CI/CD pipeline (tag workflow) |

`Dockerfile.ci` is minimal by design — it copies only the built output, producing the smallest possible image with no build tools or source code included.

---

## Base Image Selection

| Runtime | Recommended Base | Notes |
|---|---|---|
| Node.js (Next.js) | `gcr.io/distroless/nodejs20-debian12` | No shell, no package manager; compatible with Next.js standalone output |
| .NET | `mcr.microsoft.com/dotnet/aspnet:<version>-alpine` | Runtime only, no SDK |

Avoid `latest` tags on base images. Pin to a specific version tag or digest for reproducibility.

---

## Key Principles

- **Validate early, build late** — lint and test on PR open; binary and image built only on release tag
- **Build once per release** — the image built on tag is the image that reaches all environments
- **Immutable tags** — every image tag maps to exactly one digest, forever; nothing is overwritten
- **Automated semver** — release-please owns all version bumps; no manual changes to version files
- **Independent component versioning** — a change to one component never bumps another
- **Co-located ownership** — each component owns its `Dockerfile.ci` and Helm chart; no central chart repo
- **Single registry** — both container images and Helm charts are stored as OCI artifacts in GHCR
- **GitOps separation** — CI never deploys directly; it updates configuration and ArgoCD does the rest
- **Minimal images** — only the runtime and built output ship in the final image; no build tools, no source
- **Scoped caches** — each component manages its own cache; monorepo components are fully independent
