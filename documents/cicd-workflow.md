# CI/CD Workflow — Node UI App

## Overview

This app always runs in a container. The container image is the deployable unit.
All validation happens on PR open. Merge is promotion only — nothing is rebuilt.

```
PR Open   →  install → lint → test → build binary → build & push Docker image (tagged: sha-<SHA>)
PR Merge  →  re-tag image → sign → SBOM → bump Helm chart → PR to GitOps repo → ArgoCD syncs
```

---

## PR Open (`pull_request: opened, synchronize, reopened`)

Triggered on every push to an open PR targeting `main` or `develop`.

### 1. Install Dependencies
- Restore npm cache keyed on `package-lock.json` hash
- Run `npm ci` (clean, reproducible install)
- Cache `node_modules` for downstream jobs

### 2. Lint
- Restore `node_modules` from cache
- Run `npm run lint`
- Fails fast — no point building if code doesn't pass lint

### 3. Unit Tests
- Restore `node_modules` from cache
- Run `npm test`
- Runs in parallel with lint

### 4. Build Binary
- Restore `node_modules` from cache
- Run `npm run build`
- Outputs to `dist/` (or equivalent)
- Only runs after lint and test pass

### 5. Build & Push Docker Image
- Build image using `docker/build-push-action`
- Base image: `gcr.io/distroless/nodejs20-debian12` (no shell, minimal attack surface)
- Only `dist/` is copied in — no source, no `node_modules`
- Push to GHCR tagged as `sha-<SHORT_SHA>`
- Requires `permissions: packages: write` on the job
- The SHA tag is the artifact — it travels through the rest of the pipeline unchanged

> **This is the validation gate.** If the image builds and pushes cleanly, the PR is considered deployable.

---

## PR Merge (`push` to `main` or `develop`)

No code is rebuilt. The image validated during the PR is promoted.

### 1. Re-tag & Push Image
- Pull the existing image by `sha-<SHA>` (the merge commit SHA)
- Re-tag for the target environment:
  - `develop` merge → tag as `staging`
  - `main` merge → tag as `production`
- Push the new tag to GHCR
- The image digest is identical — provable continuity from PR to deploy

### 2. Sign Image (cosign)
- Sign the promoted image using `sigstore/cosign-installer` + `cosign sign`
- Signature is stored in GHCR alongside the image (no separate registry needed)
- Proves the image originated from this CI pipeline and was not tampered with
- Keyless signing via GitHub OIDC — no long-lived secrets required

### 3. Generate & Attach SBOM
- Generate a Software Bill of Materials using `syft` or `docker buildx`
- Attach SBOM to the image manifest in GHCR
- Documents every dependency shipped inside the container
- Required by many compliance frameworks (SLSA, SOC2, FedRAMP)

### 4. Bump Helm Chart in GitOps Repo
- Check out the GitOps repo (separate repo containing Helm charts and ArgoCD config)
- Update `image.tag` in the appropriate `values` file:
  - `develop` merge → `values-staging.yaml`
  - `main` merge → `values-production.yaml`
- Commit the change with a meaningful message, e.g.:
  `chore: bump image tag to sha-<SHA> for staging`

### 5. Open PR in GitOps Repo
- Open a pull request in the GitOps repo with the Helm chart change
- For `develop` → staging: auto-merge (no manual approval)
- For `main` → production: require manual approval via PR review or GitHub environment protection rules
- ArgoCD watches the GitOps repo and syncs automatically once the PR is merged

> **ArgoCD is never pushed to directly.** CI proposes the change; ArgoCD pulls it. This is the core GitOps principle.

---

## Image Tagging Strategy

| Event | Tag Applied | Purpose |
|---|---|---|
| PR push | `sha-<SHORT_SHA>` | Immutable, ties image to exact commit |
| Merge to `develop` | `staging` | Current staging image |
| Merge to `main` | `production` | Current production image |
| Release tag (optional) | `v1.2.3` | Human-readable release anchor |

---

## Monorepo Notes

- All jobs use a top-level `env.WORKING_DIR` pointing to the Node app's path
- `defaults.run.working-directory` is set per job to avoid repeating `cd`
- `cache-dependency-path` points to the app's own `package-lock.json` so cache keys are scoped correctly
- The Dockerfile context is set to `WORKING_DIR`

---

## Key Principles

- **Build once, promote the artifact** — the Docker image built on PR open is the same image that reaches production
- **Shift left** — all validation (lint, test, image build) happens before merge, not after
- **GitOps separation** — CI never deploys directly; it updates configuration and ArgoCD does the rest
- **Minimal image** — distroless base, only `dist/` copied in, no build tools in the final layer
- **Keyless signing** — cosign with GitHub OIDC, no long-lived secrets
