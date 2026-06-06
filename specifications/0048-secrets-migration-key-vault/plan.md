# Implementation Plan — Secrets Migration to Azure Key Vault (0048)

## Tasks

- [ ] T001 [Domain] Define `IKeyVaultSecretLoader` interface — `source/Testurio.Core/Interfaces/IKeyVaultSecretLoader.cs`
- [ ] T002 [Infra] Implement `KeyVaultSecretLoader` with retry (3× exponential back-off) — `source/Testurio.Infrastructure/KeyVault/KeyVaultSecretLoader.cs`
- [ ] T003 [Infra] Implement `NullKeyVaultSecretLoader` (development no-op returning empty string) — `source/Testurio.Infrastructure/KeyVault/NullKeyVaultSecretLoader.cs`
- [ ] T004 [Infra] Add `InfrastructureSecrets` class (CosmosConnectionString, ServiceBusConnectionString, BlobStorageConnectionString) — `source/Testurio.Infrastructure/Options/InfrastructureSecrets.cs`
- [ ] T005 [Infra] Add `AnthropicSecrets` class (ApiKey) — `source/Testurio.Infrastructure/Options/AnthropicSecrets.cs`
- [ ] T006 [Infra] Add `AzureOpenAISecrets` class (ApiKey) — `source/Testurio.Infrastructure/Options/AzureOpenAISecrets.cs`
- [ ] T007 [Infra] Add `StripeSecrets` class (SecretKey, WebhookSecret) — `source/Testurio.Infrastructure/Options/StripeSecrets.cs`
- [ ] T008 [Infra] Remove secret fields from `InfrastructureOptions` (CosmosConnectionString, ServiceBusConnectionString, BlobStorageConnectionString) — `source/Testurio.Infrastructure/DependencyInjection.cs`
- [ ] T009 [Infra] Remove `ApiKey` from `AnthropicOptions` — `source/Testurio.Infrastructure/DependencyInjection.cs`
- [ ] T010 [Infra] Remove `ApiKey` from `AzureOpenAIOptions` — `source/Testurio.Infrastructure/Options/AzureOpenAIOptions.cs`
- [ ] T011 [Infra] Remove `SecretKey` and `WebhookSecret` from `StripeOptions` — `source/Testurio.Infrastructure/Stripe/StripeOptions.cs`
- [ ] T012 [Infra] Add `AddKeyVaultSecretLoader` DI extension that registers `KeyVaultSecretLoader` in production and `NullKeyVaultSecretLoader` in development — `source/Testurio.Infrastructure/DependencyInjection.cs`
- [ ] T013 [Infra] Add `AddInfrastructureSecrets` startup helper: resolves `InfrastructureSecrets` via `IKeyVaultSecretLoader` (production) or local config (development) and registers as singleton — `source/Testurio.Infrastructure/DependencyInjection.cs`
- [ ] T014 [Infra] Add `AddAnthropicSecrets` startup helper: resolves `AnthropicSecrets` via `IKeyVaultSecretLoader` (production) or local config (development) and registers as singleton — `source/Testurio.Infrastructure/DependencyInjection.cs`
- [ ] T015 [Infra] Add `AddAzureOpenAISecrets` startup helper: resolves `AzureOpenAISecrets` via `IKeyVaultSecretLoader` (production) or local config (development) and registers as singleton — `source/Testurio.Infrastructure/DependencyInjection.cs`
- [ ] T016 [Infra] Add `AddStripeSecrets` startup helper: resolves `StripeSecrets` via `IKeyVaultSecretLoader` (production) or local config (development) and registers as singleton — `source/Testurio.Infrastructure/DependencyInjection.cs`
- [ ] T017 [Infra] Update `CosmosClient` singleton factory in `DependencyInjection.AddInfrastructure` to source connection string from `InfrastructureSecrets` instead of `InfrastructureOptions` — `source/Testurio.Infrastructure/DependencyInjection.cs`
- [ ] T018 [Infra] Update `ServiceBusClient` singleton factory to source connection string from `InfrastructureSecrets` — `source/Testurio.Infrastructure/DependencyInjection.cs`
- [ ] T019 [Infra] Update `BlobServiceClient` singleton factory to source connection string from `InfrastructureSecrets` — `source/Testurio.Infrastructure/DependencyInjection.cs`
- [ ] T020 [Infra] Update `AddAnthropicClient` to source `ApiKey` from `AnthropicSecrets` instead of `AnthropicOptions` — `source/Testurio.Infrastructure/DependencyInjection.cs`
- [ ] T021 [Infra] Update `AddAzureOpenAI` to source `ApiKey` from `AzureOpenAISecrets` instead of `AzureOpenAIOptions` — `source/Testurio.Infrastructure/DependencyInjection.cs`
- [ ] T022 [Infra] Update `StripeService` to source `SecretKey` and `WebhookSecret` from `StripeSecrets` instead of `StripeOptions` — `source/Testurio.Infrastructure/Stripe/StripeService.cs`
- [ ] T023 [API] Call `AddKeyVaultSecretLoader`, `AddInfrastructureSecrets`, `AddAnthropicSecrets`, and `AddStripeSecrets` at startup before `AddInfrastructure()` — `source/Testurio.Api/Program.cs`
- [ ] T024 [API] Remove the existing `KeyVault:Uri`-based `ISecretResolver` registration block (now handled by `AddKeyVaultSecretLoader`) and add `ISecretResolver` wiring that delegates to `IKeyVaultSecretLoader` for project-level secrets — `source/Testurio.Api/Program.cs`
- [ ] T025 [API] Remove inline `Claude:ApiKey` / `Claude:ModelId` configuration reads for the prompt-check `AnthropicGenerationClient`; source `ApiKey` from `AnthropicSecrets` singleton — `source/Testurio.Api/Program.cs`
- [ ] T026 [App] Call `AddKeyVaultSecretLoader`, `AddInfrastructureSecrets`, `AddAnthropicSecrets`, and `AddAzureOpenAISecrets` at startup before `AddInfrastructure()` — `source/Testurio.Worker/Program.cs`
- [ ] T027 [App] Remove the existing `KeyVault:Uri`-based `ISecretResolver` registration block from Worker and add `ISecretResolver` wiring consistent with the updated API pattern — `source/Testurio.Worker/Program.cs`
- [ ] T028 [Config] Update `.env.example` — annotate all 7 Key Vault-backed variables, add dual-source explanation header, add missing `AzureOpenAI__ApiKey` entry — `.env.example`
- [ ] T029 [Test] Unit tests for `KeyVaultSecretLoader`: retry behaviour on transient failure, fast-fail on secret-not-found, correct secret name mapping — `tests/Testurio.UnitTests/Infrastructure/KeyVaultSecretLoaderTests.cs`
- [ ] T030 [Test] Unit tests for `NullKeyVaultSecretLoader`: always returns empty string, never calls Azure SDK — `tests/Testurio.UnitTests/Infrastructure/NullKeyVaultSecretLoaderTests.cs`
- [ ] T031 [Test] Unit tests for `*Secrets` helpers: verify that in development mode each `Add*Secrets` extension reads from `IConfiguration` and produces the correct singleton; in production mode it reads from `IKeyVaultSecretLoader` — `tests/Testurio.UnitTests/Infrastructure/SecretsRegistrationTests.cs`
- [ ] T032 [Test] Integration test: `Testurio.Api` starts cleanly with `NullKeyVaultSecretLoader` and local `.env` values; all previously registered `IOptions<T>` still validate on start — `tests/Testurio.IntegrationTests/Startup/ApiStartupSecretsTests.cs`
- [ ] T033 [Test] Integration test: `Testurio.Worker` starts cleanly with `NullKeyVaultSecretLoader` and local `.env` values — `tests/Testurio.IntegrationTests/Startup/WorkerStartupSecretsTests.cs`

## Rationale

### Layer ordering

`[Domain]` first (T001): `IKeyVaultSecretLoader` is the new interface that all infrastructure and host code depends on. It belongs in `Testurio.Core` so pipeline projects can reference it without a circular dependency on `Testurio.Infrastructure`.

`[Infra]` next (T002–T022): All concrete implementations, new `*Secrets` classes, and DI extension methods live in `Testurio.Infrastructure`. The sequence within this layer matters:
1. Concrete loader implementations (T002–T003) must exist before the DI helpers reference them.
2. New `*Secrets` classes (T004–T007) must exist before factories reference them.
3. Removal of secret fields from existing options classes (T008–T011) must happen alongside, not before, step 2 — otherwise compilation would break the consumers.
4. DI extension methods (T012–T016) are added after all types they reference are stable.
5. Factory updates (T017–T022) are last within `[Infra]` because they depend on both the new `*Secrets` singletons and the updated options classes being in place.

`[API]` and `[App]` (T023–T027): `Program.cs` changes are made after `Testurio.Infrastructure` is internally consistent. The API and Worker host startup code must call the new DI extensions before calling `AddInfrastructure()`, because `AddInfrastructure` now depends on `*Secrets` singletons being registered.

`[Config]` (T028): `.env.example` is updated independently of code; no compilation dependency. Done last in the non-test sequence for clarity.

`[Test]` last (T029–T033): Tests are written after all implementation layers are complete, per project QA rules.

### Cross-feature dependencies

- **Feature 0044** provisions the Key Vault resource and grants `Key Vault Secrets User` to the Managed Identities. This feature depends on 0044 having been deployed before production startup; the application code does not check for vault existence — it simply fails fast if the vault is unreachable, which is correct behaviour.
- **Feature 0017** introduced `ISecretResolver`, `KeyVaultSecretResolver`, and `PassthroughSecretResolver` for project-level credential secrets. Feature 0048 introduces `IKeyVaultSecretLoader` as a separate, simpler abstraction for startup-time application secret loading. The two interfaces serve different concerns: `ISecretResolver` handles per-project credential URIs; `IKeyVaultSecretLoader` handles global application secrets. The existing `ISecretResolver` / `KeyVaultSecretResolver` registrations in `Program.cs` are reworked so they delegate to `IKeyVaultSecretLoader` rather than constructing a `SecretClient` independently, reducing Key Vault client duplication.
- **Features 0025–0031** (pipeline stages) depend on `ILlmGenerationClient` and `IEmbeddingService`, which are registered via `AddAnthropicClient` and `AddAzureOpenAI`. Those registrations are updated in T020–T021 to source secrets from the new `*Secrets` singletons. Pipeline stages themselves require no changes.
- **Feature 0046** (`IPlanEnforcementService`) and **Feature 0047** (Cosmos-backed prompt templates) depend on `CosmosClient` and `ServiceBusClient` being registered correctly. T017–T019 ensure those factories continue to produce valid clients.

### Architectural decisions

**Separate `*Secrets` from `*Options`**: The .NET `IOptions<T>` + `ValidateDataAnnotations` pattern is designed for configuration that can be bound from `appsettings.json`. Secrets loaded asynchronously from Key Vault at startup do not fit the `IConfiguration` binding model cleanly. The cleanest approach is to treat secrets as separately registered singletons (`InfrastructureSecrets`, etc.) populated by an explicit async startup method, while keeping non-secret fields in the existing `*Options` classes bound via `IConfiguration`.

**`IKeyVaultSecretLoader` in `Testurio.Core`**: Placing the interface in `Core` allows future pipeline stages that might need direct secret access to reference it without taking a dependency on `Testurio.Infrastructure`. It also keeps the interface testable in isolation.

**Retry in `KeyVaultSecretLoader`**: Managed Identity token acquisition can have cold-start latency of 1–3 seconds in Azure Container Apps. Three retries with exponential back-off (1s, 2s, 4s) cover transient IMDS delays without indefinitely blocking startup.

**No Bicep changes**: Key Vault provisioning, secret placeholder creation, and Managed Identity role assignments were all addressed in feature 0044. Feature 0048 is purely an application-code change.

## Layer Tags

| Tag | Scope |
|-----|-------|
| `[Migration]` | EF Core migration files |
| `[Domain]` | Entities, interfaces, value objects — `Testurio.Core` |
| `[Infra]` | Repositories, EF config, DI registration — `Testurio.Infrastructure` |
| `[App]` | DTOs, services, validators |
| `[API]` | Minimal API endpoints, route groups, middleware — `Testurio.Api` |
| `[Config]` | App configuration, constants, feature flags |
| `[UI]` | Types, API clients, hooks, MSW handlers, components, pages, i18n translation keys, route registration |
| `[Test]` | Unit, integration, and frontend component test files |
