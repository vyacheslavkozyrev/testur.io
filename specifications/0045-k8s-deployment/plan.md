# Implementation Plan — Self-Hosted Kubernetes Deployment (0045)

## Tasks

- [x] T001 [Config] Author `source/Testurio.Worker/Dockerfile` — verify the existing multi-stage Dockerfile (from feature 0044) is present and correct; no changes needed if AC-046–AC-050 of feature 0044 are satisfied — `source/Testurio.Worker/Dockerfile`
- [x] T002 [Config] Author `source/Testurio.Api/Dockerfile` — multi-stage build (`sdk:10.0` → `aspnet:10.0`), `dotnet publish -c Release`, non-root user, exposes port `8080`, sets `DOTNET_ENVIRONMENT=Production` and `ASPNETCORE_URLS=http://+:8080` — `source/Testurio.Api/Dockerfile`
- [x] T003 [Config] Author `source/Testurio.Web/Dockerfile` — multi-stage build (`node:20-alpine` build stage, `gcr.io/distroless/nodejs20-debian12` runtime), `npm ci --ignore-scripts` + `npm run build`, copies only `.next/standalone`, `.next/static`, `public/`, exposes port `3000`, sets `NODE_ENV=production` and `HOSTNAME=0.0.0.0` — `source/Testurio.Web/Dockerfile`
- [x] T004 [Infra] Author `infra/k8s/base/namespace.yaml` — `Namespace` named `testurio` — `infra/k8s/base/namespace.yaml`
- [x] T005 [Infra] Author `infra/k8s/base/rbac.yaml` — `ServiceAccount` `testurio-deployer`, `Role` with least-privilege verbs on deployments/services/configmaps/secrets/replicasets, `RoleBinding` — `infra/k8s/base/rbac.yaml`
- [ ] T006 [Infra] Author `infra/k8s/base/web/configmap.yaml` — `ConfigMap` with `NEXT_PUBLIC_API_URL` and `NODE_ENV=production` placeholders — `infra/k8s/base/web/configmap.yaml`
- [ ] T007 [Infra] Author `infra/k8s/base/web/secret.yaml` — Secret template with `NEXTAUTH_SECRET` and `NEXTAUTH_URL` set to `<REPLACE>`, with comment block warning against committing real values — `infra/k8s/base/web/secret.yaml`
- [ ] T008 [Infra] Author `infra/k8s/base/web/deployment.yaml` — `Deployment` for web, `replicas: 2`, image placeholder, probes on port `3000`, resource requests/limits, `envFrom` ConfigMap + Secret, `imagePullSecrets: regcred`, `RollingUpdate maxUnavailable: 0 maxSurge: 1` — `infra/k8s/base/web/deployment.yaml`
- [ ] T009 [Infra] Author `infra/k8s/base/web/service.yaml` — `ClusterIP` Service exposing port `3000` — `infra/k8s/base/web/service.yaml`
- [ ] T010 [Infra] Author `infra/k8s/base/api/configmap.yaml` — `ConfigMap` with `DOTNET_ENVIRONMENT`, `ASPNETCORE_URLS`, `Cosmos__Endpoint`, `ServiceBus__Namespace`, `AzureAdB2C__Domain`, `AzureAdB2C__ClientId`, `AzureAdB2C__TenantId` placeholders — `infra/k8s/base/api/configmap.yaml`
- [ ] T011 [Infra] Author `infra/k8s/base/api/secret.yaml` — Secret template with `Cosmos__ConnectionString`, `ServiceBus__ConnectionString`, `Stripe__SecretKey`, `Stripe__WebhookSecret`, `AzureAdB2C__ClientSecret`, `Anthropic__ApiKey` set to `<REPLACE>` — `infra/k8s/base/api/secret.yaml`
- [ ] T012 [Infra] Author `infra/k8s/base/api/deployment.yaml` — `Deployment` for API, `replicas: 2`, image placeholder, probes on `GET /health` port `8080`, resource requests/limits, `envFrom` ConfigMap + Secret, `imagePullSecrets: regcred`, `RollingUpdate maxUnavailable: 0 maxSurge: 1` — `infra/k8s/base/api/deployment.yaml`
- [ ] T013 [Infra] Author `infra/k8s/base/api/service.yaml` — `ClusterIP` Service exposing port `8080` — `infra/k8s/base/api/service.yaml`
- [ ] T014 [Infra] Author `infra/k8s/base/worker/configmap.yaml` — `ConfigMap` with `DOTNET_ENVIRONMENT`, `Cosmos__Endpoint`, `ServiceBus__Namespace`, `AzureAdB2C__TenantId` placeholders — `infra/k8s/base/worker/configmap.yaml`
- [ ] T015 [Infra] Author `infra/k8s/base/worker/secret.yaml` — Secret template with `Cosmos__ConnectionString`, `ServiceBus__ConnectionString`, `Anthropic__ApiKey`, `AzureAiSearch__ApiKey` set to `<REPLACE>` — `infra/k8s/base/worker/secret.yaml`
- [ ] T016 [Infra] Author `infra/k8s/base/worker/deployment.yaml` — `Deployment` for worker, `replicas: 1`, image placeholder, no HTTP probes, resource requests/limits, `envFrom` ConfigMap + Secret, `imagePullSecrets: regcred`, `Recreate` strategy — `infra/k8s/base/worker/deployment.yaml`
- [ ] T017 [Infra] Author `infra/k8s/base/kustomization.yaml` — lists namespace, rbac, and all service resources (web deployment/service/configmap, api deployment/service/configmap, worker deployment/configmap) — `infra/k8s/base/kustomization.yaml`
- [ ] T018 [Infra] Author `infra/k8s/overlays/staging/kustomization.yaml` — extends base, `images` field for all three services pointing to `ghcr.io/vyacheslavkozyrev/testurio-{web,api,worker}:latest` (tag overridden by CI at deploy time) — `infra/k8s/overlays/staging/kustomization.yaml`
- [ ] T019 [Infra] Author `infra/k8s/overlays/staging/configmap-patch.yaml` — patches `NEXT_PUBLIC_API_URL` and `Cosmos__Endpoint` for staging environment (operator-filled placeholders) — `infra/k8s/overlays/staging/configmap-patch.yaml`
- [ ] T020 [Infra] Author `infra/k8s/overlays/production/kustomization.yaml` — extends base, `images` field for all three services, applies worker replica and resource limit patch — `infra/k8s/overlays/production/kustomization.yaml`
- [ ] T021 [Infra] Author `infra/k8s/overlays/production/configmap-patch.yaml` — patches `NEXT_PUBLIC_API_URL` and `Cosmos__Endpoint` for production environment — `infra/k8s/overlays/production/configmap-patch.yaml`
- [ ] T022 [Infra] Author `infra/k8s/overlays/production/worker-patch.yaml` — strategic merge patch increasing worker `replicas` to `2` and resource limits to `cpu: 2000m`, `memory: 2Gi` — `infra/k8s/overlays/production/worker-patch.yaml`
- [ ] T023 [Config] Update `infra/k8s/README.md` — documents pre-deployment sequence: (1) create namespace, (2) apply RBAC, (3) substitute and apply secrets, (4) create `regcred` image pull secret, (5) apply kustomize overlay; includes cosign verification command — `infra/k8s/README.md`
- [ ] T024 [Config] Update `.gitignore` — add `infra/k8s/**/*.secret.yaml` exclusion pattern so operator-filled secret files are never accidentally committed; keep template `secret.yaml` files tracked — `.gitignore`
- [ ] T025 [Config] Author `.github/workflows/k8s-pr.yml` — triggers on PR to `develop`/`main`; four sequential jobs (`install` → `lint` → `test` → `build-and-push`); `build-and-push` pushes all three images to GHCR tagged `sha-<SHORT_SHA>` using `GITHUB_TOKEN`; `packages: write` permission scoped to `build-and-push` job only — `.github/workflows/k8s-pr.yml`
- [ ] T026 [Config] Author `.github/workflows/k8s-promote-staging.yml` — triggers on push to `develop`; re-tags `sha-<SHORT_SHA>` → `staging` via `docker buildx imagetools create`; cosign keyless sign via OIDC; syft SBOM generation and cosign attestation; opens GitOps repo PR bumping staging Helm chart `image.tag`; requires `id-token: write` + `packages: write` — `.github/workflows/k8s-promote-staging.yml`
- [ ] T027 [Config] Author `.github/workflows/k8s-promote-production.yml` — triggers on push to `main`; verifies cosign signature on `sha-<SHORT_SHA>` before proceeding; re-tags → `production`; cosign sign; syft SBOM; opens GitOps repo PR for production Helm chart; requires `id-token: write` + `packages: write` — `.github/workflows/k8s-promote-production.yml`

## Rationale

**Dockerfiles before manifests and workflows (T001–T003 before T004–T027).** Every subsequent step — Kubernetes manifests, Kustomize overlays, and CI/CD workflows — references the image produced by the Dockerfiles. The Worker Dockerfile from feature 0044 is verified first (T001) because it already exists; Api (T002) and Web (T003) are new and must be authored before their images can be referenced in manifests.

**Web Dockerfile uses distroless runtime (T003).** Per the team's cicd-workflow.md document, the Web service uses `gcr.io/distroless/nodejs20-debian12` as its runtime base. The Api and Worker services use `mcr.microsoft.com/dotnet/aspnet:10.0`, consistent with feature 0044 and the cicd-workflow.md document.

**Namespace and RBAC before all other manifests (T004–T005 before T006–T016).** All workload resources are namespaced; the `Namespace` resource must exist before any resource in that namespace is applied. The `ServiceAccount` and `Role` must exist before CI/CD workflows can use the kubeconfig. Authoring these first also ensures the `kustomization.yaml` (T017) can list them as the first resources.

**ConfigMaps before Deployments (T006, T010, T014 before T008, T012, T016).** Each `Deployment` references its `ConfigMap` via `envFrom`. Authoring the `ConfigMap` first confirms the key names used in the `envFrom` reference are correct and complete before the `Deployment` is written.

**Secrets before Deployments (T007, T011, T015 before T008, T012, T016).** Same reasoning as ConfigMaps — each `Deployment` references a `Secret` via `envFrom`. The template `Secret` is authored before the `Deployment` so the secret name is known and consistent.

**Services after Deployments (T009, T013 after T008, T012).** Each `Service` selects pods by the labels defined in the `Deployment` pod template. Authoring the `Deployment` first confirms the label set, which is then mirrored in the `Service` selector. The Worker has no `Service` (T016 only).

**Kustomize base after all resources (T017 after T004–T016).** `kustomization.yaml` simply lists the resources already authored; it must be written last to avoid referencing files that do not yet exist.

**Overlays after base (T018–T022 after T017).** Kustomize overlays extend the base; the base `kustomization.yaml` must be stable before overlays reference it.

**README and .gitignore after all manifest files (T023–T024 after T017).** The README documents the final directory structure and apply sequence. The `.gitignore` pattern can only be confirmed correct once the directory layout is settled.

**CI/CD workflows after Dockerfiles and manifests (T025–T027 after T001–T024).** The workflows reference Dockerfile paths, image names derived from the GHCR org (Q5: `ghcr.io/vyacheslavkozyrev/...`), and Kustomize overlay paths. All of these must be settled before the workflow YAML is authored.

**PR workflow before promote workflows (T025 before T026–T027).** The promote workflows re-tag the image produced by the PR workflow (`sha-<SHORT_SHA>`). The PR workflow must exist and its image naming convention must be established first, because the promote workflows derive the source tag from the same `SHORT_SHA` logic.

**Staging promote before production promote (T026 before T027).** The production workflow (T027) verifies the cosign signature applied by the staging workflow (T026) before re-tagging. T027 cannot be authored correctly until T026's signing step and the resulting attestation format are known.

**No `[Test]` tasks.** This feature consists entirely of Dockerfiles, Kubernetes YAML, Kustomize configuration, and GitHub Actions workflow YAML. There are no application logic units to unit-test. Validation is performed by: `docker build` (Dockerfiles), `kubectl kustomize` (manifests), and a dry-run CI execution against a sandbox cluster. Integration validation is the operator's responsibility.

**Cross-feature dependencies.**

- **Feature 0044 (Azure Infrastructure):** The Worker Dockerfile authored in feature 0044 (T016 of that plan) is reused directly by this feature. T001 of this plan verifies it rather than re-creating it.
- **Feature 0044 (deploy-worker.yml):** The existing Azure workflow files (deploy-web.yml, deploy-api.yml, deploy-worker.yml) are left untouched per Q4 answer `a`. The new k8s-specific workflows (`k8s-pr.yml`, `k8s-promote-staging.yml`, `k8s-promote-production.yml`) are entirely separate files targeting GHCR rather than ACR.
- **Features 0001–0031 (all backend features):** The environment variable names in `configmap.yaml` files (e.g. `Cosmos__Endpoint`, `ServiceBus__Namespace`) must match the configuration keys expected by `Testurio.Api` and `Testurio.Worker`. These are established by the application configuration in those features and are assumed stable before T010 and T014 are implemented.
- **Feature 0017 (Testing Environment Access):** The Worker manifests (T016) must not restrict egress in a way that prevents the cluster from reaching client staging environments. No NetworkPolicy restricting egress is included in this feature; egress control is the cluster operator's responsibility.

## Layer Tags

| Tag | Scope |
|-----|-------|
| `[Domain]` | Entities, interfaces, value objects — `Testurio.Core` |
| `[Infra]` | Kubernetes manifests, Kustomize base and overlays — `infra/k8s/` |
| `[App]` | DTOs, services, validators |
| `[API]` | Minimal API endpoints, route groups, middleware — `Testurio.Api` |
| `[Config]` | Dockerfiles, GitHub Actions workflow YAML, README, .gitignore updates |
| `[UI]` | Types, API clients, hooks, MSW handlers, components, pages, i18n translation keys |
| `[Test]` | Unit, integration, and frontend component test files |
