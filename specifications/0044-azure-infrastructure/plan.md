# Implementation Plan — Azure Infrastructure & CI/CD Deployment (0044)

## Tasks

- [x] T001 [Infra] Author `infra/modules/keyvault.bicep` — RBAC access model, soft-delete 90 days, purge protection, outputs `vaultName` and `vaultUri` — `infra/modules/keyvault.bicep`
- [x] T002 [Infra] Author `infra/modules/appinsights.bicep` — Log Analytics workspace + workspace-based Application Insights instance, outputs `instrumentationKey` and `connectionString` — `infra/modules/appinsights.bicep`
- [ ] T003 [Infra] Author `infra/modules/acr.bicep` — Container Registry, admin disabled, outputs `loginServer` and `resourceId` — `infra/modules/acr.bicep`
- [ ] T004 [Infra] Author `infra/modules/natgateway.bicep` — public IP prefix, NAT Gateway resource, subnet association parameter, outputs `publicIpAddresses` array — `infra/modules/natgateway.bicep`
- [ ] T005 [Infra] Author `infra/modules/staticwebapp.bicep` — Static Web Apps resource, outputs `defaultHostname` and `deploymentTokenSecretName` — `infra/modules/staticwebapp.bicep`
- [ ] T006 [Infra] Author `infra/modules/appservice.bicep` — App Service plan + Web App, .NET 10 runtime, HTTPS-only, system-assigned Managed Identity, outputs `appServiceName`, `principalId`, and `defaultHostname` — `infra/modules/appservice.bicep`
- [ ] T007 [Infra] Author `infra/modules/containerapps.bicep` — Container Apps environment + Worker Container App, ACR image reference, Key Vault reference env vars, system-assigned Managed Identity, outputs `containerAppName` and `principalId` — `infra/modules/containerapps.bicep`
- [ ] T008 [Infra] Author `infra/modules/aisearch.bicep` — Azure AI Search, semantic ranker enabled, vector index for `storyEmbedding` (1536 dims, cosine), environment-driven SKU, outputs `searchEndpoint` and `resourceId` — `infra/modules/aisearch.bicep`
- [ ] T009 [Infra] Author `infra/modules/frontdoor.bicep` — Front Door Standard profile, origins for Static Web Apps and App Service, empty WAF policy, outputs `frontDoorEndpointHostname` — `infra/modules/frontdoor.bicep`
- [ ] T010 [Infra] Author `infra/modules/apim.bicep` — API Management, consumption (dev) or developer (prod) tier driven by `environment` parameter, outputs `gatewayUrl` — `infra/modules/apim.bicep`
- [ ] T011 [Infra] Author `infra/modules/adb2c.bicep` — reference-only module (no resource creation), accepts `tenantDomain` and `clientId` as inputs and passes them through as outputs for `main.bicep` wiring — `infra/modules/adb2c.bicep`
- [ ] T012 [Infra] Author root `infra/main.bicep` — `environment` parameter (`'dev'|'prod'`), `prefix` parameter, orchestrates all modules with environment-driven SKU variables, wires Key Vault secret placeholders, grants Managed Identity role assignments (`Key Vault Secrets User`) to App Service and Container Apps identities — `infra/main.bicep`
- [ ] T013 [Infra] Author `infra/dev.bicepparam` — parameter file targeting `dev` environment with cost-optimised overrides — `infra/dev.bicepparam`
- [ ] T014 [Infra] Author `infra/prod.bicepparam` — parameter file targeting `prod` environment with production-tier overrides — `infra/prod.bicepparam`
- [ ] T015 [Config] Author `infra/bootstrap.sh` — Bash + Azure CLI script: validates `az` (2.50+) and `jq` presence, accepts `--env dev|prod`, creates/verifies resource group, creates/updates OIDC Federated Credential on the designated App Registration for the correct branch, seeds Key Vault secret placeholders with `__REPLACE__` sentinel values, outputs GitHub secret names and values to configure — `infra/bootstrap.sh`
- [ ] T016 [Infra] Author `source/Testurio.Worker/Dockerfile` — multi-stage build (`sdk:10.0` → `aspnet:10.0`), non-root user, no EXPOSE, `DOTNET_ENVIRONMENT=Production` default — `source/Testurio.Worker/Dockerfile`
- [ ] T017 [Config] Author `.github/workflows/deploy-web.yml` — OIDC auth, trigger on `develop`→dev and `main`→prod, `npm ci` + `npm run build` in `source/Testurio.Web/`, `NEXT_PUBLIC_API_URL` injected per environment, deploy via `Azure/static-web-apps-deploy@v1` with token retrieved at runtime via Azure CLI — `.github/workflows/deploy-web.yml`
- [ ] T018 [Config] Author `.github/workflows/deploy-api.yml` — OIDC auth, trigger on `develop`→dev and `main`→prod, `dotnet publish` of `Testurio.Api.csproj`, deploy via `azure/webapps-deploy@v3`, App Service name from GitHub Actions variable — `.github/workflows/deploy-api.yml`
- [ ] T019 [Config] Author `.github/workflows/deploy-worker.yml` — OIDC auth, trigger on `develop`→dev and `main`→prod, Docker build from `source/Testurio.Worker/Dockerfile`, push to ACR tagged with Git SHA, `az containerapp update` with new image tag, Container App name and ACR login server from GitHub Actions variables — `.github/workflows/deploy-worker.yml`

## Rationale

**Key Vault and observability modules first (T001–T002).** Every other module either emits secrets into Key Vault or emits diagnostics to App Insights. Authoring these first means `main.bicep` can reference their outputs immediately when wiring dependent modules.

**ACR before Container Apps (T003 before T007).** `containerapps.bicep` references the ACR login server to set the initial image reference. The ACR module must exist before the Container Apps module so `main.bicep` can pass the `loginServer` output as a parameter.

**NAT Gateway standalone (T004).** The NAT Gateway is associated with the Container Apps subnet at the environment level. It has no dependencies on other modules beyond the location parameter, so it can be authored in parallel with the early service modules.

**Static Web Apps before Front Door (T005 before T009).** `frontdoor.bicep` takes the Static Web Apps `defaultHostname` as an origin; the SWA module must be defined first.

**App Service before Front Door (T006 before T009).** Similarly, Front Door requires the App Service `defaultHostname` as an origin.

**Container Apps after ACR and Key Vault (T007 after T001, T003).** The Container Apps module wires Key Vault references into environment variables and references the ACR login server for the initial image. Both upstream outputs must be available.

**AI Search independent (T008).** AI Search has no dependency on other modules beyond location and SKU parameters. It is ordered after the foundational modules purely for readability.

**`main.bicep` after all modules (T012 after T001–T011).** The root orchestrator is written last because it imports outputs from every module. Writing it before all modules are complete would require placeholder values that would later need to be replaced.

**`.bicepparam` files after `main.bicep` (T013–T014).** Parameter files are validated against the `main.bicep` parameter declarations; they cannot be correctly authored until `main.bicep` is stable.

**`bootstrap.sh` after `main.bicep` and parameter files (T015 after T012–T014).** The script references the resource group names and App Registration client ID that correspond to the deployed architecture. These names are finalised in `main.bicep` and the `.bicepparam` files.

**Dockerfile before CI/CD workflows (T016 before T017–T019).** The `deploy-worker.yml` workflow references the Dockerfile path; the file must exist before the workflow YAML is authored to confirm the path is correct.

**Three independent workflow files (T017–T019).** The three deployment targets (web, API, worker) are deployed independently per the architecture — Static Web Apps, App Service, and Container Apps have separate deployment mechanisms. Keeping them in separate workflow files means a frontend-only change does not trigger an API or Worker build, and a failed API deployment does not block a Worker deployment.

**No `[Test]` tasks.** This feature consists entirely of infrastructure-as-code, shell scripts, and CI/CD YAML. There are no application logic units to unit-test. Validation is performed by `az bicep build` (linting) and by running `bootstrap.sh` in a dry-run mode against a sandbox subscription. Integration testing (actual Azure deployment) is the responsibility of the operator running the bootstrap and deployment workflows in a `dev` environment.

**Cross-feature dependencies.**

- **Features 0001–0031 (all backend features):** `appservice.bicep` and `containerapps.bicep` must expose the correct environment variable names expected by `Testurio.Api` and `Testurio.Worker`. These names are established by the application configuration in those features; this plan assumes they are stable before T006 and T007 are implemented.
- **Feature 0017 (Testing Environment Access Configuration):** `natgateway.bicep` outputs the fixed egress IP addresses that the operator publishes in documentation and that clients add to their firewall allowlists. The NAT Gateway module (T004) must be complete before feature 0017's documentation references can be finalised.
- **Feature 0027 (Memory Retrieval) and Feature 0028 (Test Generator Agents):** `aisearch.bicep` (T008) must provision a compatible vector index (1536 dimensions, cosine distance) matching the embedding schema defined in those features.

## Layer Tags

| Tag | Scope |
|-----|-------|
| `[Domain]` | Entities, interfaces, value objects — `Testurio.Core` |
| `[Infra]` | Bicep modules, root orchestrator, parameter files — `infra/` |
| `[App]` | DTOs, services, validators |
| `[API]` | Minimal API endpoints, route groups, middleware — `Testurio.Api` |
| `[Config]` | Bootstrap scripts, GitHub Actions workflow YAML, Dockerfile |
| `[UI]` | Types, API clients, hooks, MSW handlers, components, pages, i18n translation keys |
| `[Test]` | Unit, integration, and frontend component test files |
