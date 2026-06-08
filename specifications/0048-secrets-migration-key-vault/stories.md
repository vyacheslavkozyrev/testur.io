# User Stories — Secrets Migration to Azure Key Vault (0048)

## Out of Scope

The following are explicitly **not** part of this feature:

- Key Vault provisioning or Managed Identity role assignment — infrastructure is defined in feature 0044 (Bicep modules already wire the vault and grant `Key Vault Secrets User` to App Service and Container Apps)
- Secret rotation policies or expiry configuration — manual rotation only; automation is deferred
- Audit logging of who read or updated a secret — covered by Azure Key Vault diagnostic settings, not application code
- Per-environment secret namespacing (e.g. `dev/` vs `prod/` prefixes) — Key Vault instance is per-environment by convention; no prefix logic in application code
- Frontend (.env) or Next.js configuration — only backend services (API + Worker) are in scope; `NEXT_PUBLIC_*` variables remain in Static Web Apps app settings
- Automated secret seeding from CI/CD pipeline — the `bootstrap.sh` one-time manual process (feature 0044) remains the mechanism for populating initial values
- Migration of project-level credentials (Basic Auth, header tokens) — those are already stored in Key Vault via `ISecretResolver` (feature 0017)
- Azure OpenAI Endpoint URL — it is a non-sensitive endpoint, not a secret

---

## Stories

### US-001: Cosmos DB Connection String Loaded from Key Vault

**As a** platform engineer
**I want** the Cosmos DB connection string to be loaded from Azure Key Vault at application startup
**So that** plaintext connection strings with account keys never appear in environment variables or app settings in production

#### Acceptance Criteria

- [ ] AC-001: In production (`!IsDevelopment()`), both `Testurio.Api` and `Testurio.Worker` retrieve the Cosmos DB connection string from Key Vault using Managed Identity before constructing the `CosmosClient`
- [ ] AC-002: The secret name used is `cosmos-connection-string`
- [ ] AC-003: If the secret is missing or Key Vault is unreachable at startup, both services fail fast with a `KeyVaultException` logged at `Critical` level and the process exits
- [ ] AC-004: In development (`IsDevelopment()`), the connection string continues to be read from `.env` / user secrets as `Infrastructure__CosmosConnectionString`
- [ ] AC-005: The `InfrastructureOptions.CosmosConnectionString` property is removed from the options class; it must not be bindable from plain configuration in production
- [ ] AC-006: The `.env.example` entry for `Infrastructure__CosmosConnectionString` is annotated with a comment indicating it is Key Vault-backed in production

#### Edge Cases

- If `KeyVault:Uri` is not set in production configuration, the app must throw `InvalidOperationException` at startup before attempting any Key Vault call

---

### US-002: Service Bus Connection String Loaded from Key Vault

**As a** platform engineer
**I want** the Service Bus connection string to be loaded from Azure Key Vault at application startup
**So that** the shared access key never appears in plain configuration

#### Acceptance Criteria

- [ ] AC-007: In production, both `Testurio.Api` and `Testurio.Worker` retrieve the Service Bus connection string from Key Vault using Managed Identity
- [ ] AC-008: The secret name used is `servicebus-connection-string`
- [ ] AC-009: If the secret is missing or Key Vault is unreachable at startup, both services fail fast with a `Critical`-level log and process exit
- [ ] AC-010: In development, the connection string continues to be read from `.env` / user secrets as `Infrastructure__ServiceBusConnectionString`
- [ ] AC-011: The `InfrastructureOptions.ServiceBusConnectionString` property is removed; it must not be bindable from plain configuration in production
- [ ] AC-012: The `.env.example` entry for `Infrastructure__ServiceBusConnectionString` is annotated as Key Vault-backed in production

---

### US-003: Blob Storage Connection String Loaded from Key Vault

**As a** platform engineer
**I want** the Blob Storage connection string to be loaded from Azure Key Vault at application startup
**So that** the storage account key never appears in plain configuration

#### Acceptance Criteria

- [ ] AC-013: In production, both services retrieve the Blob Storage connection string from Key Vault
- [ ] AC-014: The secret name used is `blob-storage-connection-string`
- [ ] AC-015: If the secret is missing at startup, both services fail fast
- [ ] AC-016: In development, the connection string is read from `.env` / user secrets as `Infrastructure__BlobStorageConnectionString`
- [ ] AC-017: The `InfrastructureOptions.BlobStorageConnectionString` property is removed; it must not be bindable from plain configuration in production
- [ ] AC-018: The `.env.example` entry is annotated as Key Vault-backed in production

---

### US-004: Anthropic Claude API Key Loaded from Key Vault

**As a** platform engineer
**I want** the Anthropic Claude API key to be loaded from Azure Key Vault at application startup
**So that** the API key is never exposed in logs, environment variables, or deployment manifests

#### Acceptance Criteria

- [ ] AC-019: In production, `Testurio.Worker` (and `Testurio.Api` for its optional prompt-check client) retrieve the Anthropic API key from Key Vault
- [ ] AC-020: The secret name used is `anthropic-api-key`
- [ ] AC-021: If the secret is missing in the Worker at startup, the Worker fails fast; in the API it logs a `Warning` and continues (the prompt-check endpoint fails gracefully at runtime)
- [ ] AC-022: In development, the key is read from `.env` / user secrets as `Claude__ApiKey`
- [ ] AC-023: The `AnthropicOptions.ApiKey` property is removed; it must not be bindable from plain configuration in production
- [ ] AC-024: The `.env.example` entry for `Claude__ApiKey` is annotated as Key Vault-backed in production

---

### US-005: Stripe Secret Key and Webhook Secret Loaded from Key Vault

**As a** platform engineer
**I want** the Stripe secret key and webhook secret to be loaded from Azure Key Vault at application startup
**So that** live Stripe credentials are never stored in App Service app settings or deployment pipelines

#### Acceptance Criteria

- [ ] AC-025: In production, `Testurio.Api` retrieves the Stripe secret key and webhook secret from Key Vault
- [ ] AC-026: The secret names used are `stripe-secret-key` and `stripe-webhook-secret`
- [ ] AC-027: If either secret is missing at startup in production, `Testurio.Api` fails fast
- [ ] AC-028: In development, values are read from `.env` / user secrets as `Stripe__SecretKey` and `Stripe__WebhookSecret`
- [ ] AC-029: `StripeOptions.SecretKey` and `StripeOptions.WebhookSecret` are removed from the options class; they must not be bindable from plain configuration in production
- [ ] AC-030: Both `.env.example` entries are annotated as Key Vault-backed in production
- [ ] AC-031: `Testurio.Worker` does not call Key Vault for Stripe secrets — it has no Stripe dependency

---

### US-006: Azure OpenAI API Key Loaded from Key Vault

**As a** platform engineer
**I want** the Azure OpenAI API key to be loaded from Azure Key Vault at application startup
**So that** the embedding service key is not exposed in deployment configuration

#### Acceptance Criteria

- [ ] AC-032: In production, `Testurio.Worker` retrieves the Azure OpenAI API key from Key Vault
- [ ] AC-033: The secret name used is `azure-openai-api-key`
- [ ] AC-034: If the secret is missing at startup, the Worker fails fast
- [ ] AC-035: In development, the key is read from `.env` / user secrets as `AzureOpenAI__ApiKey`
- [ ] AC-036: `AzureOpenAIOptions.ApiKey` is removed; it must not be bindable from plain configuration in production
- [ ] AC-037: The `.env.example` entry for `AzureOpenAI__ApiKey` is annotated as Key Vault-backed in production

---

### US-007: Key Vault Secret Loading is Centralised in Infrastructure

**As a** backend engineer
**I want** all Key Vault secret reads to be handled through a single `IKeyVaultSecretLoader` service registered in `Testurio.Infrastructure`
**So that** `Program.cs` files in the API and Worker remain clean and there is a single place to add retry logic or circuit-breaking

#### Acceptance Criteria

- [ ] AC-038: A new `IKeyVaultSecretLoader` interface is defined in `Testurio.Core` with a method `Task<string> GetSecretAsync(string secretName, CancellationToken ct)`
- [ ] AC-039: `KeyVaultSecretLoader` is implemented in `Testurio.Infrastructure` using `DefaultAzureCredential` and the `KeyVault:Uri` configuration value
- [ ] AC-040: `KeyVaultSecretLoader.GetSecretAsync` retries up to 3 times with exponential back-off (1s, 2s, 4s) before throwing
- [ ] AC-041: Both `Testurio.Api` and `Testurio.Worker` call `IKeyVaultSecretLoader` at startup to resolve secrets before constructing options objects
- [ ] AC-042: In development, a `NullKeyVaultSecretLoader` implementation returns an empty string (callers fall back to local configuration)
- [ ] AC-043: DI registration is added to `Testurio.Infrastructure.DependencyInjection` with a `AddKeyVaultSecretLoader(IConfiguration)` extension that selects the correct implementation based on `IHostEnvironment.IsDevelopment()`

---

### US-008: Sensitive Options Classes Restructured for Dual-Source Binding

**As a** backend engineer
**I want** the options classes (`InfrastructureOptions`, `AnthropicOptions`, `AzureOpenAIOptions`, `StripeOptions`) to separate secret fields from non-secret fields
**So that** non-secret fields continue to bind from standard configuration while secret fields are always populated from Key Vault

#### Acceptance Criteria

- [ ] AC-044: Each options class that previously held secret fields is split into two classes: `<Name>Options` (non-secret, binds from configuration) and `<Name>Secrets` (secret, populated from Key Vault at startup)
- [ ] AC-045: `InfrastructureSecrets` holds: `CosmosConnectionString`, `ServiceBusConnectionString`, `BlobStorageConnectionString`
- [ ] AC-046: `AnthropicSecrets` holds: `ApiKey`
- [ ] AC-047: `AzureOpenAISecrets` holds: `ApiKey`
- [ ] AC-048: `StripeSecrets` holds: `SecretKey`, `WebhookSecret`
- [ ] AC-049: All `*Secrets` classes are registered as singletons populated during startup, before `IOptions<T>` validation runs
- [ ] AC-050: `*Options` classes retain only non-secret fields and continue to use `ValidateDataAnnotations` + `ValidateOnStart`
- [ ] AC-051: `[Required]` attributes are removed from all secret fields that have been moved out of options classes

---

### US-009: Local Development Continues to Use .env / User Secrets

**As a** backend developer
**I want** local development to work without any Key Vault or Azure credentials
**So that** I can run the API and Worker locally using `.env` values or .NET user secrets without provisioning Azure infrastructure

#### Acceptance Criteria

- [ ] AC-052: When `IsDevelopment()` is true, `InfrastructureSecrets`, `AnthropicSecrets`, `AzureOpenAISecrets`, and `StripeSecrets` are populated from `.env` / user secrets using the existing environment variable names
- [ ] AC-053: No Azure credential (`DefaultAzureCredential`, `ManagedIdentityCredential`) is instantiated in development mode
- [ ] AC-054: Running `docker-compose up` with a valid `.env` file starts the API and Worker successfully without any Key Vault configuration
- [ ] AC-055: The developer documentation comment in `.env.example` clearly distinguishes which values are Key Vault-backed in production versus which remain as plain app settings in all environments

---

### US-010: .env.example Updated to Document Key Vault-Backed Secrets

**As a** new developer onboarding to the project
**I want** the `.env.example` file to clearly mark which variables are loaded from Key Vault in production
**So that** I understand which values I need to set locally versus which are only needed in the Azure deployment pipeline

#### Acceptance Criteria

- [ ] AC-056: Each of the 7 Key Vault-backed variables in `.env.example` includes a comment: `# [KEY VAULT in production — secret name: <name>]`
- [ ] AC-057: Non-secret variables (database names, queue names, container names, model IDs, public URLs, Stripe price IDs) have no Key Vault annotation
- [ ] AC-058: The file includes a top-level comment block explaining the dual-source pattern: local dev uses `.env`; production uses Key Vault for secrets, app settings for everything else
- [ ] AC-059: `AzureOpenAI__ApiKey` is included in `.env.example` with a Key Vault annotation (it was previously missing from the example file)
