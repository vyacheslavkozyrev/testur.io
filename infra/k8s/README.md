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

Run these steps once on a fresh cluster before the first ArgoCD sync or manual apply.

### 0. Bootstrap namespace and RBAC

```bash
kubectl apply -f infra/k8s/base/namespace.yaml
kubectl apply -f infra/k8s/base/rbac.yaml
```

### 1. Substitute and apply Secrets

The `secret.yaml` files in each service directory are templates — all values are `<REPLACE>` placeholders and must never be committed with real values.

Copy each template to a `.secret.yaml` variant (git-ignored), substitute the values, then apply:

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

### 2. Apply ConfigMaps

```bash
kubectl apply -f infra/k8s/base/web/configmap.yaml
kubectl apply -f infra/k8s/base/api/configmap.yaml
kubectl apply -f infra/k8s/base/worker/configmap.yaml
```

### 3. Apply Deployments and Services

```bash
kubectl apply -k infra/k8s/overlays/staging   # or overlays/production
```

To preview the rendered manifests without applying:

```bash
kubectl kustomize infra/k8s/overlays/staging
```

### 4. Create the GHCR image pull secret (`regcred`)

All images are pulled from GitHub Container Registry (`ghcr.io`). Create the `regcred` secret used by all Deployments:

```bash
kubectl create secret docker-registry regcred \
  --docker-server=ghcr.io \
  --docker-username=<GITHUB_USERNAME> \
  --docker-password=<GITHUB_PAT_WITH_READ_PACKAGES> \
  --namespace=testurio
```

## Configuring the `KUBECONFIG` GitHub Secret

The CI/CD workflows authenticate to the cluster as the `testurio-deployer` service account — never as cluster-admin. To generate the kubeconfig:

```bash
# 1. Get the service account token secret name (Kubernetes 1.24+ requires manual secret creation)
kubectl apply -f - <<EOF
apiVersion: v1
kind: Secret
metadata:
  name: testurio-deployer-token
  namespace: testurio
  annotations:
    kubernetes.io/service-account.name: testurio-deployer
type: kubernetes.io/service-account-token
EOF

# 2. Extract the token and CA cert
TOKEN=$(kubectl get secret testurio-deployer-token -n testurio -o jsonpath='{.data.token}' | base64 -d)
CA=$(kubectl get secret testurio-deployer-token -n testurio -o jsonpath='{.data.ca\.crt}')
SERVER=$(kubectl config view --minify -o jsonpath='{.clusters[0].cluster.server}')

# 3. Build the kubeconfig
cat <<EOF > testurio-deployer-kubeconfig.yaml
apiVersion: v1
kind: Config
clusters:
- cluster:
    certificate-authority-data: ${CA}
    server: ${SERVER}
  name: home-cluster
contexts:
- context:
    cluster: home-cluster
    namespace: testurio
    user: testurio-deployer
  name: testurio-deployer@home-cluster
current-context: testurio-deployer@home-cluster
users:
- name: testurio-deployer
  user:
    token: ${TOKEN}
EOF

# 4. Add to GitHub Actions secrets
gh secret set KUBECONFIG --body "$(cat testurio-deployer-kubeconfig.yaml)"

# 5. Delete the local file — do not commit it
rm testurio-deployer-kubeconfig.yaml
```

The `testurio-deployer` Role grants only the permissions required for deployments, services, configmaps, secrets, and replicasets in the `testurio` namespace — no cluster-wide access.

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
