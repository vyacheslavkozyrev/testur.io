# Kubernetes Deployment — Testurio

This directory contains raw Kubernetes manifests and Kustomize overlays for deploying Testurio to a self-hosted cluster. Azure-hosted backing services (Cosmos DB, Service Bus, Azure AD B2C, AI Search) continue to run on Azure; the cluster workloads call them over the network.

## Directory Structure

```
infra/k8s/
├── base/
│   ├── namespace.yaml          # testurio Namespace
│   ├── rbac.yaml               # ServiceAccount, Role, RoleBinding
│   ├── kustomization.yaml      # Kustomize base resource list
│   ├── web/
│   │   ├── configmap.yaml
│   │   ├── secret.yaml         # template — never commit real values
│   │   ├── deployment.yaml
│   │   └── service.yaml
│   ├── api/
│   │   ├── configmap.yaml
│   │   ├── secret.yaml         # template — never commit real values
│   │   ├── deployment.yaml
│   │   └── service.yaml
│   └── worker/
│       ├── configmap.yaml
│       ├── secret.yaml         # template — never commit real values
│       └── deployment.yaml
└── overlays/
    ├── staging/
    │   ├── kustomization.yaml
    │   └── configmap-patch.yaml
    └── production/
        ├── kustomization.yaml
        ├── configmap-patch.yaml
        └── worker-patch.yaml   # increases replicas and resource limits
```

## Pre-Deployment Sequence

### 1. Create the namespace

```bash
kubectl apply -f infra/k8s/base/namespace.yaml
```

### 2. Apply RBAC

```bash
kubectl apply -f infra/k8s/base/rbac.yaml
```

### 3. Substitute and apply Secrets

The `secret.yaml` files in each service directory are templates. They contain `<REPLACE>` placeholders and must never be committed with real values.

For each service, copy the template to a `.secret.yaml` variant (which is git-ignored), substitute the values, and apply:

```bash
# Web
cp infra/k8s/base/web/secret.yaml infra/k8s/base/web/web.secret.yaml
# Edit web.secret.yaml — replace all <REPLACE> placeholders
kubectl apply -f infra/k8s/base/web/web.secret.yaml

# API
cp infra/k8s/base/api/secret.yaml infra/k8s/base/api/api.secret.yaml
# Edit api.secret.yaml — replace all <REPLACE> placeholders
kubectl apply -f infra/k8s/base/api/api.secret.yaml

# Worker
cp infra/k8s/base/worker/secret.yaml infra/k8s/base/worker/worker.secret.yaml
# Edit worker.secret.yaml — replace all <REPLACE> placeholders
kubectl apply -f infra/k8s/base/worker/worker.secret.yaml
```

### 4. Create the GHCR image pull secret

All images are pushed to GitHub Container Registry (`ghcr.io`). Create the `regcred` pull secret used by all Deployments:

```bash
kubectl create secret docker-registry regcred \
  --docker-server=ghcr.io \
  --docker-username=<GITHUB_USERNAME> \
  --docker-password=<GITHUB_PAT> \
  --namespace=testurio
```

### 5. Apply the Kustomize overlay

Replace `staging` with `production` as appropriate:

```bash
kubectl apply -k infra/k8s/overlays/staging
```

To preview the rendered manifests without applying:

```bash
kubectl kustomize infra/k8s/overlays/staging
```

## Image Verification with cosign

The CI/CD pipeline signs all promoted images using cosign keyless signing via OIDC (Sigstore). Verify a signed image before deploying:

```bash
cosign verify \
  --certificate-identity-regexp="https://github.com/vyacheslavkozyrev/testurio/.github/workflows/k8s-promote-staging.yml" \
  --certificate-oidc-issuer="https://token.actions.githubusercontent.com" \
  ghcr.io/vyacheslavkozyrev/testurio-api:staging
```

Replace `staging` with `production` and update the workflow path for production images.

## CI/CD Image Tag Convention

| Branch / event        | Image tag                          | Workflow                        |
| --------------------- | ---------------------------------- | ------------------------------- |
| Pull request to any   | `sha-<7-char-SHA>`                 | `k8s-pr.yml`                    |
| Merge to `develop`    | `staging` (re-tagged from SHA)     | `k8s-promote-staging.yml`       |
| Merge to `main`       | `production` (re-tagged from SHA)  | `k8s-promote-production.yml`    |

CI overrides the `latest` tag in the overlay at deploy time using:

```bash
kustomize edit set image ghcr.io/vyacheslavkozyrev/testurio-web=ghcr.io/vyacheslavkozyrev/testurio-web:sha-<SHORT_SHA>
```
