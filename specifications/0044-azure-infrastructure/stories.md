# User Stories — Azure Infrastructure & CI/CD Deployment (0044)

## Out of Scope

The following are explicitly **not** part of this feature:

- Azure AD B2C tenant creation and policy configuration — the B2C tenant is referenced by resource ID / domain only; identity policy authoring is a manual one-time operation
- Stripe account provisioning — Stripe is an external SaaS; only the secret references are wired through Key Vault
- Per-tenant infrastructure — all resources are shared; physical tenant isolation is explicitly excluded (see architecture decisions)
- Custom domain DNS management — Front Door origin and hostname configuration is included but external DNS cut-over is out of scope
- Blue/green or canary deployment strategies — each environment has a single active slot
- Database migration execution — Bicep provisions the Cosmos account, database, and containers; data migrations are handled by application startup code
- GitHub repository creation or branch protection rule configuration

---

## Stories

### US-001: Bicep Module Library for All Azure Services

**As a** platform engineer
**I want to** have a complete, parameterised Bicep module for every Azure service Testurio uses
**So that** any environment can be reproduced from source control without manual portal clicks

#### Acceptance Criteria

- [ ] AC-001: Separate Bicep module files exist under `infra/modules/` for every service listed in the architecture: Static Web Apps, App Service, Container Apps, Cosmos DB, Service Bus, Key Vault, AI Search, App Insights, ACR, NAT Gateway, Front Door, API Management, and Azure AD B2C reference module
- [ ] AC-002: Each module accepts `location`, a name/prefix parameter, and any service-specific required parameters; no module hard-codes a resource name or location
- [ ] AC-003: Each module exports at least the resource `id` and any connection-string or endpoint values needed by dependent modules as `output` declarations
- [ ] AC-004: All modules pass `az bicep build` without errors or warnings
- [ ] AC-005: The existing `infra/modules/cosmos.bicep` and `infra/modules/servicebus.bicep` are preserved exactly as authored and extended only if required by this feature's output wiring

#### Edge Cases

- If a service does not support a given region, the module must surface the error at deployment time via a clear `az deployment group create` failure message rather than silently falling back to a different region

---

### US-002: Root Orchestrator with Environment Parameters

**As a** platform engineer
**I want to** deploy the entire Testurio stack to either `dev` or `prod` with a single command
**So that** I do not need to invoke individual module deployments manually

#### Acceptance Criteria

- [ ] AC-006: A root `infra/main.bicep` file orchestrates all modules using `module` declarations; it does not duplicate resource definitions already in the modules
- [ ] AC-007: `main.bicep` accepts an `environment` parameter with allowed values `'dev'` and `'prod'`; this single parameter drives all environment-specific defaults (SKUs, replica counts, retention periods)
- [ ] AC-008: A `dev` environment uses cost-optimised SKUs: Cosmos DB serverless, Service Bus Standard, App Service B2 plan, Container Apps consumption plan, AI Search Free tier
- [ ] AC-009: A `prod` environment uses production SKUs: Cosmos DB provisioned throughput (400 RU/s), Service Bus Premium, App Service P2v3 plan, Container Apps dedicated plan, AI Search Standard tier
- [ ] AC-010: Two `.bicepparam` files exist — `infra/dev.bicepparam` and `infra/prod.bicepparam` — allowing individual parameter overrides without modifying `main.bicep`
- [ ] AC-011: Running `az deployment group create --template-file infra/main.bicep --parameters infra/dev.bicepparam` completes idempotently — re-running against an existing deployment produces no changes unless parameters differ
- [ ] AC-012: All resource names are derived from a `prefix` parameter combined with the `environment` value (e.g. `testurio-dev-cosmos`, `testurio-prod-cosmos`) to ensure uniqueness across environments

#### Edge Cases

- Deploying `prod` `.bicepparam` against the `dev` resource group must fail with a clear parameter validation error, not silently create prod-tier resources in the wrong group

---

### US-003: Key Vault Secret Provisioning and Managed Identity Wiring

**As a** platform engineer
**I want to** have all application secrets stored in Key Vault and accessed via Managed Identity
**So that** no plaintext credentials appear in pipelines, Bicep parameters, or application configuration

#### Acceptance Criteria

- [ ] AC-013: The Key Vault module provisions a vault with RBAC access model (not access policies)
- [ ] AC-014: The following secrets are pre-provisioned as Key Vault secret placeholders (empty string value, `__REPLACE__` sentinel) ready for the operator to fill: `stripe-secret-key`, `stripe-webhook-secret`, `anthropic-api-key`, `cosmos-connection-string`, `servicebus-connection-string`, `adb2c-client-secret`
- [ ] AC-015: App Service and Container Apps are provisioned with system-assigned Managed Identities
- [ ] AC-016: Both Managed Identities are granted the `Key Vault Secrets User` role on the Key Vault via role assignment resources in `main.bicep`
- [ ] AC-017: Application configuration references secrets using Key Vault references (`@Microsoft.KeyVault(SecretUri=...)`) — no secret values appear in App Service app settings or Container Apps environment variables
- [ ] AC-018: A `bootstrap.sh` script documents the one-time manual step required to seed real secret values after initial deployment: `az keyvault secret set --vault-name <name> --name <secret> --value <value>`

#### Edge Cases

- If Managed Identity role assignment propagation is delayed, the application must retry Key Vault reads with exponential back-off on startup; the Bicep module adds a `dependsOn` to ensure the role assignment is created before the App Service starts

---

### US-004: GitHub Actions Workflow — Frontend (Next.js → Static Web Apps)

**As a** developer
**I want to** have a GitHub Actions workflow that deploys the Next.js frontend to Azure Static Web Apps on every merge to `develop` or `main`
**So that** frontend changes are live within minutes of merging without any manual deployment step

#### Acceptance Criteria

- [ ] AC-019: Workflow file `.github/workflows/deploy-web.yml` triggers on `push` to `develop` (deploys to `dev`) and `push` to `main` (deploys to `prod`)
- [ ] AC-020: The workflow authenticates to Azure using OIDC Federated Credentials — no long-lived client secrets stored in GitHub secrets; only `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, and `AZURE_SUBSCRIPTION_ID` are required
- [ ] AC-021: The workflow runs `npm ci` and `npm run build` in `source/Testurio.Web/` and deploys the output using the `Azure/static-web-apps-deploy@v1` action
- [ ] AC-022: The Static Web Apps deployment token is retrieved at runtime via the Azure CLI (`az staticwebapp secrets list`) using the OIDC identity — it is not stored as a static GitHub secret
- [ ] AC-023: The workflow sets the `NEXT_PUBLIC_API_URL` environment variable to the appropriate API endpoint for the target environment (`dev` or `prod`) during the build step
- [ ] AC-024: A failed deployment does not leave a partial release; the Static Web Apps deployment is atomic
- [ ] AC-025: The workflow uses `ubuntu-latest` runners and no self-hosted infrastructure

#### Edge Cases

- If the build step fails, the deploy step must not run; the workflow must fail fast on any step error (`set -e` equivalent via `continue-on-error: false`)

---

### US-005: GitHub Actions Workflow — API (ASP.NET Core → App Service)

**As a** developer
**I want to** have a GitHub Actions workflow that builds and deploys the ASP.NET Core API to Azure App Service on every merge to `develop` or `main`
**So that** API changes reach the correct environment automatically

#### Acceptance Criteria

- [ ] AC-026: Workflow file `.github/workflows/deploy-api.yml` triggers on `push` to `develop` (deploys to `dev` App Service) and `push` to `main` (deploys to `prod` App Service)
- [ ] AC-027: The workflow authenticates via OIDC Federated Credentials using the same GitHub secrets as US-004 (`AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`)
- [ ] AC-028: The workflow runs `dotnet publish -c Release -o ./publish source/Testurio.Api/Testurio.Api.csproj` and deploys the output using `azure/webapps-deploy@v3`
- [ ] AC-029: The App Service name is resolved from a per-environment GitHub Actions variable (`APP_SERVICE_NAME_DEV` / `APP_SERVICE_NAME_PROD`) — not hardcoded in the workflow YAML
- [ ] AC-030: The workflow does not set any secret values as environment variables; all secrets are read from Key Vault at runtime via Key Vault references already configured in App Service settings
- [ ] AC-031: On a failed publish step the workflow exits before the deploy step runs

#### Edge Cases

- If the App Service is in a stopped state, the deploy action must start it; the `azure/webapps-deploy` action handles this natively

---

### US-006: GitHub Actions Workflow — Worker (.NET Worker → Container Apps via ACR)

**As a** developer
**I want to** have a GitHub Actions workflow that builds the Worker Docker image, pushes it to ACR, and updates the Container Apps revision on every merge to `develop` or `main`
**So that** pipeline worker changes are deployed without manual image tagging or container restart

#### Acceptance Criteria

- [ ] AC-032: Workflow file `.github/workflows/deploy-worker.yml` triggers on `push` to `develop` (deploys to `dev` Container App) and `push` to `main` (deploys to `prod` Container App)
- [ ] AC-033: The workflow authenticates to Azure and to ACR via OIDC Federated Credentials — no ACR admin credentials stored in GitHub secrets; the OIDC identity is granted `AcrPush` role on the ACR resource
- [ ] AC-034: The workflow builds the Docker image from `source/Testurio.Worker/Dockerfile`, tags it with the Git SHA (`ghcr.io` is not used — ACR is the registry), and pushes to ACR
- [ ] AC-035: After the push, the workflow updates the Container App to use the new image tag via `az containerapp update --image <acr-login-server>/<image>:<sha>`
- [ ] AC-036: The Container App name and ACR login server are resolved from per-environment GitHub Actions variables — not hardcoded in the workflow YAML
- [ ] AC-037: If the `az containerapp update` step fails, the previous Container App revision continues serving traffic; the failed revision is not activated

#### Edge Cases

- If ACR push succeeds but `az containerapp update` fails, the operator can manually re-run `az containerapp update` with the same image tag to recover without rebuilding the image

---

### US-007: Bootstrap Script for OIDC and Resource Group Setup

**As a** platform engineer setting up the deployment infrastructure for the first time
**I want to** run a single script that creates the resource groups, registers the Federated Credential on the Azure AD application, and outputs the GitHub secrets to configure
**So that** the one-time Azure setup is reproducible and documented as code

#### Acceptance Criteria

- [ ] AC-038: `infra/bootstrap.sh` is a Bash + Azure CLI script that accepts `--env` (`dev` or `prod`) as a required argument
- [ ] AC-039: The script creates (or verifies the existence of) the target resource group in the configured region
- [ ] AC-040: The script creates (or updates) a Federated Credential on the designated Azure AD application registration for the specified GitHub repository and branch (`refs/heads/develop` for `dev`, `refs/heads/main` for `prod`)
- [ ] AC-041: The script outputs the three GitHub secret values the operator must configure: `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`
- [ ] AC-042: The script outputs the GitHub Actions variable values the operator must configure: `APP_SERVICE_NAME_DEV` / `APP_SERVICE_NAME_PROD`, `CONTAINER_APP_NAME_DEV` / `CONTAINER_APP_NAME_PROD`, `ACR_LOGIN_SERVER`
- [ ] AC-043: The script is idempotent — running it twice against the same environment produces the same result with no errors
- [ ] AC-044: The script requires `az` CLI (2.50+) and `jq`; it checks for both at startup and exits with an informative message if either is missing
- [ ] AC-045: The script does NOT store any secret values; it only provisions the identity plumbing and emits names/IDs that the operator pastes into GitHub

#### Edge Cases

- If the resource group already exists in a different region, the script prints a warning and exits without modifying the existing group

---

### US-008: Worker Dockerfile

**As a** platform engineer
**I want to** have a production-ready Dockerfile for the .NET Worker Service
**So that** the CI/CD workflow can build a self-contained image that runs in Container Apps

#### Acceptance Criteria

- [ ] AC-046: `source/Testurio.Worker/Dockerfile` uses a multi-stage build: `mcr.microsoft.com/dotnet/sdk:10.0` for build and `mcr.microsoft.com/dotnet/aspnet:10.0` for the runtime stage
- [ ] AC-047: The final image runs as a non-root user
- [ ] AC-048: The Dockerfile copies only the published output into the runtime stage — no source code, `.git` directory, or test projects are included
- [ ] AC-049: The image exposes no inbound HTTP port (the Worker is a consumer, not a server); the `EXPOSE` instruction is omitted
- [ ] AC-050: The image sets `DOTNET_ENVIRONMENT` to `Production` by default; this can be overridden by a Container Apps environment variable

#### Edge Cases

- If the build stage fails (e.g. compilation error), the runtime image is never produced and the push step does not run

---

### US-009: Missing Bicep Modules for Remaining Azure Services

**As a** platform engineer
**I want to** have Bicep modules for the services not yet covered (`cosmos.bicep` and `servicebus.bicep` exist, eleven more are needed)
**So that** `main.bicep` can reference all services without any module being absent

#### Acceptance Criteria

- [ ] AC-051: `infra/modules/staticwebapp.bicep` provisions an Azure Static Web Apps resource and outputs the deployment token name and default hostname
- [ ] AC-052: `infra/modules/appservice.bicep` provisions an App Service plan and Web App with the correct .NET runtime, system-assigned Managed Identity enabled, and HTTPS-only enforced
- [ ] AC-053: `infra/modules/containerapps.bicep` provisions a Container Apps environment and a single Container App for the Worker, with the correct ACR image reference, environment variables wired to Key Vault references, and system-assigned Managed Identity enabled
- [ ] AC-054: `infra/modules/keyvault.bicep` provisions a Key Vault with RBAC access model, soft-delete enabled (90-day retention), and purge protection enabled
- [ ] AC-055: `infra/modules/aisearch.bicep` provisions an Azure AI Search service with the semantic ranker enabled and a vector index definition matching the `storyEmbedding` field (1536 dimensions, cosine distance)
- [ ] AC-056: `infra/modules/appinsights.bicep` provisions an Application Insights workspace-based instance linked to a Log Analytics workspace; both resources live in this module
- [ ] AC-057: `infra/modules/acr.bicep` provisions an Azure Container Registry with admin account disabled (Managed Identity access only)
- [ ] AC-058: `infra/modules/natgateway.bicep` provisions a NAT Gateway with a public IP prefix and associates it with the Container Apps subnet
- [ ] AC-059: `infra/modules/frontdoor.bicep` provisions an Azure Front Door Standard profile with origins pointing to the Static Web Apps hostname and the App Service hostname; WAF policy attachment is included but WAF rules are empty by default
- [ ] AC-060: `infra/modules/apim.bicep` provisions an API Management instance in consumption tier (dev) or developer tier (prod) with the API gateway URL output
- [ ] AC-061: `infra/modules/adb2c.bicep` is a reference-only module: it does not create a B2C tenant (not possible via ARM) but outputs the tenant domain and application client ID from input parameters, making the values available to `main.bicep` for wiring into App Service and Container Apps configuration

#### Edge Cases

- The NAT Gateway module must output the public IP address(es) so the operator can add them to client firewall allowlists and to the Testurio documentation page (feature 0017)
