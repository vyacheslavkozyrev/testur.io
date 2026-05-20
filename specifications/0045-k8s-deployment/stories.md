# User Stories — Self-Hosted Kubernetes Deployment (0045)

## Out of Scope

The following are explicitly **not** part of this feature:

- Provisioning or managing the Kubernetes cluster itself — the cluster is assumed to exist and be reachable via a `KUBECONFIG` secret
- Replacing Azure Cosmos DB, Service Bus, Azure AD B2C, AI Search, or any other Azure-hosted backing service with self-hostable alternatives — those services continue to run on Azure; the cluster simply calls them over the network
- Helm chart authoring — raw Kubernetes manifests only; Helm is the responsibility of the GitOps repo referenced in the CI/CD workflow documents
- ArgoCD installation or configuration — CI proposes manifest changes; ArgoCD pulls and applies them
- TLS certificate management or ingress controller setup — cluster operators handle ingress and certificate provisioning
- Multi-cluster or federation scenarios
- Azure-specific GitHub Actions workflows (deploy-web.yml, deploy-api.yml, deploy-worker.yml) — those remain untouched
- Database or schema migrations — handled by application startup code
- Per-tenant Kubernetes namespaces — logical multi-tenancy only, consistent with the architecture

---

## Stories

### US-001: Production-Ready Dockerfile for the Web Service (Next.js SSR)

**As a** platform engineer
**I want to** have a production-ready Dockerfile for the Next.js web application that runs a full Node.js SSR server
**So that** the web service can be containerised and deployed to any Kubernetes cluster

#### Acceptance Criteria

- [ ] AC-001: `source/Testurio.Web/Dockerfile` uses a multi-stage build: an `node:20-alpine` build stage and a `gcr.io/distroless/nodejs20-debian12` runtime stage
- [ ] AC-002: The build stage installs dependencies with `npm ci --ignore-scripts` and runs `npm run build`; only the compiled `.next/standalone` output, `.next/static`, and `public/` are copied into the runtime stage
- [ ] AC-003: The final image runs as a non-root user (distroless default non-root UID 65532)
- [ ] AC-004: The image exposes port `3000` and sets `HOSTNAME=0.0.0.0` so Next.js SSR listens on all interfaces
- [ ] AC-005: No source code, `node_modules` from the build stage, `.git` directory, test files, or dev-only config is present in the final image
- [ ] AC-006: The image sets `NODE_ENV=production` as an environment variable default

#### Edge Cases

- If the `npm run build` step fails, the runtime image is never produced and no push step runs

---

### US-002: Production-Ready Dockerfile for the API Service (ASP.NET Core)

**As a** platform engineer
**I want to** have a production-ready Dockerfile for the ASP.NET Core API
**So that** the API can be containerised and deployed to Kubernetes alongside the web and worker services

#### Acceptance Criteria

- [ ] AC-007: `source/Testurio.Api/Dockerfile` uses a multi-stage build: `mcr.microsoft.com/dotnet/sdk:10.0` for build and `mcr.microsoft.com/dotnet/aspnet:10.0` for the runtime stage
- [ ] AC-008: The build stage runs `dotnet publish -c Release -o /app/publish source/Testurio.Api/Testurio.Api.csproj`; the publish output is the only artefact copied to the runtime stage
- [ ] AC-009: The final image runs as a non-root user via `USER $APP_UID` (using the built-in .NET image non-root convention)
- [ ] AC-010: The image exposes port `8080` (the default ASP.NET Core Kestrel port in .NET 8+ non-root configuration)
- [ ] AC-011: The image sets `DOTNET_ENVIRONMENT=Production` and `ASPNETCORE_URLS=http://+:8080` as defaults
- [ ] AC-012: No source code, test projects, `.git` directory, or build-time tooling is present in the final image

#### Edge Cases

- If the `dotnet publish` step fails, the runtime image is never produced

---

### US-003: Kubernetes Manifests for the Web Service

**As a** platform engineer
**I want to** have Kubernetes manifests for the Web service
**So that** the Next.js SSR server can be deployed, scaled, and accessed within the cluster

#### Acceptance Criteria

- [ ] AC-013: `infra/k8s/base/web/deployment.yaml` defines a `Deployment` for the web service with `replicas: 2`, `image` referencing a `$(IMAGE_TAG)` placeholder, liveness and readiness probes on `GET /` port `3000`, and resource requests/limits (`cpu: 250m/500m`, `memory: 256Mi/512Mi`)
- [ ] AC-014: `infra/k8s/base/web/service.yaml` defines a `ClusterIP` `Service` exposing port `3000`
- [ ] AC-015: `infra/k8s/base/web/configmap.yaml` defines a `ConfigMap` containing non-secret environment variables: `NEXT_PUBLIC_API_URL` (placeholder `https://api.example.com`) and `NODE_ENV=production`
- [ ] AC-016: The `Deployment` references the `ConfigMap` via `envFrom` and references the web `Secret` (US-006) via `envFrom`
- [ ] AC-017: The `Deployment` specifies `imagePullSecrets` referencing a `regcred` secret for GHCR authentication
- [ ] AC-018: The `Deployment` sets `restartPolicy: Always` and uses `RollingUpdate` strategy with `maxUnavailable: 0` and `maxSurge: 1`

---

### US-004: Kubernetes Manifests for the API Service

**As a** platform engineer
**I want to** have Kubernetes manifests for the API service
**So that** the ASP.NET Core API can be deployed, scaled, and accessed within the cluster

#### Acceptance Criteria

- [ ] AC-019: `infra/k8s/base/api/deployment.yaml` defines a `Deployment` for the API with `replicas: 2`, `image` referencing a `$(IMAGE_TAG)` placeholder, liveness and readiness probes on `GET /health` port `8080`, and resource requests/limits (`cpu: 250m/500m`, `memory: 256Mi/512Mi`)
- [ ] AC-020: `infra/k8s/base/api/service.yaml` defines a `ClusterIP` `Service` exposing port `8080`
- [ ] AC-021: `infra/k8s/base/api/configmap.yaml` defines a `ConfigMap` containing non-secret environment variables: `DOTNET_ENVIRONMENT`, `ASPNETCORE_URLS`, `Cosmos__Endpoint` (placeholder), `ServiceBus__Namespace` (placeholder), `AzureAdB2C__Domain` (placeholder), `AzureAdB2C__ClientId` (placeholder), `AzureAdB2C__TenantId` (placeholder)
- [ ] AC-022: The `Deployment` references the `ConfigMap` via `envFrom` and references the API `Secret` (US-006) via `envFrom`
- [ ] AC-023: The `Deployment` specifies `imagePullSecrets` referencing `regcred`
- [ ] AC-024: The `Deployment` uses `RollingUpdate` strategy with `maxUnavailable: 0` and `maxSurge: 1`

---

### US-005: Kubernetes Manifests for the Worker Service

**As a** platform engineer
**I want to** have Kubernetes manifests for the Worker service
**So that** the .NET Worker (test pipeline) can be deployed with the correct configuration and access to backing services

#### Acceptance Criteria

- [ ] AC-025: `infra/k8s/base/worker/deployment.yaml` defines a `Deployment` for the worker with `replicas: 1`, `image` referencing a `$(IMAGE_TAG)` placeholder, no liveness/readiness HTTP probes (worker is a consumer), and resource requests/limits (`cpu: 500m/1000m`, `memory: 512Mi/1Gi`)
- [ ] AC-026: No `Service` manifest is created for the worker — it consumes from Service Bus and has no inbound traffic
- [ ] AC-027: `infra/k8s/base/worker/configmap.yaml` defines a `ConfigMap` with non-secret environment variables: `DOTNET_ENVIRONMENT`, `Cosmos__Endpoint` (placeholder), `ServiceBus__Namespace` (placeholder), `AzureAdB2C__TenantId` (placeholder)
- [ ] AC-028: The `Deployment` references the `ConfigMap` via `envFrom` and references the Worker `Secret` (US-006) via `envFrom`
- [ ] AC-029: The `Deployment` specifies `imagePullSecrets` referencing `regcred`
- [ ] AC-030: The `Deployment` uses `Recreate` update strategy — only one worker revision runs at a time to avoid duplicate pipeline processing

---

### US-006: Kubernetes Secret Templates (Plain k8s Secrets)

**As a** platform engineer
**I want to** have documented Secret manifest templates for each service
**So that** I can create the secrets in the cluster before deploying, without committing sensitive values to source control

#### Acceptance Criteria

- [ ] AC-031: `infra/k8s/base/web/secret.yaml` is a template (all values set to `<REPLACE>` placeholder) containing: `NEXTAUTH_SECRET` and `NEXTAUTH_URL`
- [ ] AC-032: `infra/k8s/base/api/secret.yaml` is a template containing: `Cosmos__ConnectionString`, `ServiceBus__ConnectionString`, `Stripe__SecretKey`, `Stripe__WebhookSecret`, `AzureAdB2C__ClientSecret`, `Anthropic__ApiKey`
- [ ] AC-033: `infra/k8s/base/worker/secret.yaml` is a template containing: `Cosmos__ConnectionString`, `ServiceBus__ConnectionString`, `Anthropic__ApiKey`, `AzureAiSearch__ApiKey`
- [ ] AC-034: Each secret template file includes a comment block at the top explaining that it must never be committed with real values and should be applied via `kubectl apply -f` after substituting values
- [ ] AC-035: A `infra/k8s/README.md` file documents the sequence: (1) substitute and apply secrets, (2) apply ConfigMaps, (3) apply Deployments and Services, (4) create `regcred` image pull secret for GHCR
- [ ] AC-036: `.gitignore` is updated so that any file matching `infra/k8s/**/*.secret.yaml` (operator-filled variants) is excluded from source control; the template `secret.yaml` files (with `<REPLACE>` placeholders) remain tracked

---

### US-007: Kustomize Base and Overlay Structure

**As a** platform engineer
**I want to** have a Kustomize base with staging and production overlays
**So that** environment-specific differences (image tags, replica counts, resource limits) are managed without duplicating the full manifest set

#### Acceptance Criteria

- [ ] AC-037: `infra/k8s/base/kustomization.yaml` lists all resources: web deployment, web service, web configmap, api deployment, api service, api configmap, worker deployment, worker configmap, plus the namespace definition
- [ ] AC-038: `infra/k8s/overlays/staging/kustomization.yaml` extends the base and sets the image tags for all three services using the Kustomize `images` field pointing to `ghcr.io/vyacheslavkozyrev/testurio-web`, `ghcr.io/vyacheslavkozyrev/testurio-api`, and `ghcr.io/vyacheslavkozyrev/testurio-worker`; the default tag is `latest` (overridden by CI via `--set-image`)
- [ ] AC-039: `infra/k8s/overlays/production/kustomization.yaml` extends the base and applies a patch that increases worker replicas to `2` and increases resource limits (`cpu: 1000m/2000m`, `memory: 1Gi/2Gi` for worker); the staging overlay uses base defaults
- [ ] AC-040: `infra/k8s/overlays/staging/configmap-patch.yaml` overrides `NEXT_PUBLIC_API_URL` and `Cosmos__Endpoint` placeholders with staging-specific values (still placeholders in the repo — the operator fills them)
- [ ] AC-041: `infra/k8s/overlays/production/configmap-patch.yaml` overrides the same fields for production
- [ ] AC-042: Running `kubectl kustomize infra/k8s/overlays/staging` produces a valid, complete manifest set with no unresolved Kustomize directives

---

### US-008: GitHub Actions — PR Build, Lint, Test, and Image Push

**As a** developer
**I want to** have a GitHub Actions workflow that runs on every pull request to build, lint, test, and push a Docker image tagged with the commit SHA
**So that** every PR proves the build is green and produces a ready-to-promote artefact

#### Acceptance Criteria

- [ ] AC-043: `.github/workflows/k8s-pr.yml` triggers on `pull_request` targeting `develop` or `main`
- [ ] AC-044: The workflow contains four sequential jobs: `install`, `lint`, `test`, `build-and-push`; each later job depends on the prior via `needs:`
- [ ] AC-045: The `install` job runs `npm ci` for `source/Testurio.Web/` and `dotnet restore` for the solution; outputs are cached between jobs using `actions/cache`
- [ ] AC-046: The `lint` job runs `npm run lint` in `source/Testurio.Web/` and `dotnet format --verify-no-changes` for the .NET solution
- [ ] AC-047: The `test` job runs `npm test -- --watchAll=false` for `source/Testurio.Web/` and `dotnet test tests/Testurio.UnitTests/` for the backend
- [ ] AC-048: The `build-and-push` job builds Docker images for all three services (Web, Api, Worker) and pushes them to GHCR tagged `sha-<SHORT_SHA>` (first 7 characters of `github.sha`); the `SHORT_SHA` is computed in a step and exposed as a job output
- [ ] AC-049: GHCR authentication uses `GITHUB_TOKEN` via `docker/login-action` with `registry: ghcr.io` — no additional secrets required
- [ ] AC-050: Image names follow the pattern `ghcr.io/vyacheslavkozyrev/testurio-<service>:sha-<SHORT_SHA>`
- [ ] AC-051: The workflow grants `packages: write` permission for the `build-and-push` job only; all other jobs use `contents: read` only
- [ ] AC-052: If any job fails, subsequent jobs are skipped and the PR check is marked failed

---

### US-009: GitHub Actions — Merge to develop Promotes Image to Staging

**As a** developer
**I want to** have a GitHub Actions workflow that triggers on merge to `develop`, re-tags the existing PR image (no rebuild), signs it with cosign, generates an SBOM, and opens a PR against the GitOps repo to bump the staging Helm chart
**So that** the exact same image built during PR review is promoted to staging without a second build

#### Acceptance Criteria

- [ ] AC-053: `.github/workflows/k8s-promote-staging.yml` triggers on `push` to `develop`
- [ ] AC-054: The workflow re-tags the image `sha-<SHORT_SHA>` → `staging` using `docker buildx imagetools create` (no rebuild); `SHORT_SHA` is derived from `github.sha`
- [ ] AC-055: The workflow signs the `staging`-tagged image using cosign keyless signing via OIDC (Sigstore); no cosign private key is stored in GitHub secrets
- [ ] AC-056: The workflow generates an SBOM for the `staging`-tagged images using `syft` and attaches it to the image in GHCR via cosign attestation
- [ ] AC-057: The workflow opens a pull request against the GitOps repository using the `peter-evans/create-pull-request` action (or equivalent `gh pr create` call), bumping the `image.tag` field in the staging Helm chart values file from the previous SHA to `sha-<SHORT_SHA>`
- [ ] AC-058: The workflow requires `id-token: write` permission for cosign OIDC and `packages: write` for GHCR re-tagging
- [ ] AC-059: If the GitOps PR step fails, the image remains signed and available in GHCR; the operator can manually trigger the GitOps PR step by re-running the workflow
- [ ] AC-060: ArgoCD is never pushed to directly from this workflow — it observes the GitOps repo and syncs when the PR is merged

---

### US-010: GitHub Actions — Merge to main Promotes Image to Production

**As a** developer
**I want to** have a GitHub Actions workflow that triggers on merge to `main`, re-tags the image to `production`, signs it, generates an SBOM, and opens a PR against the GitOps repo for the production Helm chart
**So that** production deployments go through the same build-once/promote-artifact pattern as staging

#### Acceptance Criteria

- [ ] AC-061: `.github/workflows/k8s-promote-production.yml` triggers on `push` to `main`
- [ ] AC-062: The workflow re-tags the image `sha-<SHORT_SHA>` → `production` using `docker buildx imagetools create`; `SHORT_SHA` is derived from `github.sha`
- [ ] AC-063: The workflow signs and generates an SBOM using the same cosign keyless + syft pattern as US-009, applied to the `production`-tagged images
- [ ] AC-064: The workflow opens a pull request against the GitOps repository bumping the `image.tag` in the production Helm chart values file
- [ ] AC-065: The workflow requires `id-token: write` and `packages: write` permissions
- [ ] AC-066: A production promotion requires that the `staging`-tagged image for the same SHA was previously signed — the workflow verifies the cosign signature on the `sha-<SHORT_SHA>` image before re-tagging; if verification fails, the workflow exits with an error

---

### US-011: Namespace and RBAC Foundation

**As a** platform engineer
**I want to** have a Kubernetes namespace definition and minimal RBAC for the Testurio workloads
**So that** all services run in an isolated namespace and the CI/CD service account has only the permissions it needs

#### Acceptance Criteria

- [ ] AC-067: `infra/k8s/base/namespace.yaml` defines a `Namespace` named `testurio`
- [ ] AC-068: All `Deployment`, `Service`, `ConfigMap`, and `Secret` manifests set `namespace: testurio`
- [ ] AC-069: `infra/k8s/base/rbac.yaml` defines a `ServiceAccount` named `testurio-deployer`, a `Role` granting `get`, `list`, `create`, `update`, `patch`, and `delete` on `deployments`, `services`, `configmaps`, `secrets`, and `replicasets` within the `testurio` namespace, and a `RoleBinding` binding that `Role` to the `testurio-deployer` `ServiceAccount`
- [ ] AC-070: The CI/CD `KUBECONFIG` secret in GitHub configures a kubeconfig that authenticates as the `testurio-deployer` service account — the workflow does not use a cluster-admin credential
