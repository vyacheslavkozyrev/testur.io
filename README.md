# Testurio

SaaS platform for automated software testing. Accepts webhooks from Azure DevOps and Jira, parses user stories with an LLM, generates and executes tests, and writes results back to the originating issue tracker.

## Architecture overview

| Component | Technology | Purpose |
|-----------|-----------|---------|
| `Testurio.Api` | ASP.NET Core 9 | REST API, webhook receivers (ADO, Jira), auth |
| `Testurio.Worker` | .NET Worker Service 9 | Test pipeline — Story Parser → Test Generator → Test Executor → Report Writer |
| `Testurio.Core` | .NET 9 class library | Domain models, interfaces, value objects |
| `Testurio.Infrastructure` | .NET 9 class library | Cosmos DB, Service Bus, Key Vault, Stripe clients |
| `Testurio.Web` | Next.js *(planned)* | User portal — not yet implemented |

See `documents/architecture.md` for full detail.

## Running locally

### Step 1 — Install prerequisites

| Tool | Version | Notes |
|------|---------|-------|
| [.NET SDK](https://dotnet.microsoft.com/download/dotnet/9) | 9.0+ | Verify with `dotnet --version` |
| [Azure Cosmos DB Emulator](https://learn.microsoft.com/en-us/azure/cosmos-db/local-emulator) | Latest | Replaces a real Cosmos account for local dev |
| Azure Service Bus namespace | Any tier | No local emulator exists — create a free namespace in the Azure portal |

### Step 2 — Clone and build

```bash
git clone <repo-url>
cd testur.io
dotnet restore Testurio.sln
dotnet build Testurio.sln
```

Both commands must complete with no errors before continuing.

### Step 3 — Start the Cosmos Emulator

Launch the Azure Cosmos DB Emulator from the Start menu. Wait until the tray icon turns green and the data explorer opens at `https://localhost:8081`.

Copy the connection string from the emulator's Quickstart page at `https://localhost:8081/_explorer/index.html` — use the **Primary Connection String** shown there.

### Step 4 — Create a Service Bus queue

1. In the Azure portal, create a **Service Bus namespace** (Basic tier is fine)
2. Inside it, create a **queue** named `story-test-pipeline`
3. Copy the **primary connection string** from Shared access policies → RootManageSharedAccessKey

### Step 5 — Configure the API

Configuration is loaded from `appsettings.json` → `appsettings.Development.json` → [.NET User Secrets](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets). All six keys below are validated at startup — the app will not start if any is missing.

```bash
cd source/Testurio.Api

dotnet user-secrets set "AzureAdB2C:Authority" "https://placeholder.b2clogin.com/placeholder.onmicrosoft.com/B2C_1_signupsignin/v2.0"
dotnet user-secrets set "AzureAdB2C:ClientId" "00000000-0000-0000-0000-000000000000"

dotnet user-secrets set "Infrastructure:CosmosConnectionString" "<primary connection string from https://localhost:8081/_explorer/index.html>"
dotnet user-secrets set "Infrastructure:CosmosDatabaseName" "testurio"
dotnet user-secrets set "Infrastructure:ServiceBusConnectionString" "<your-service-bus-connection-string>"
dotnet user-secrets set "Infrastructure:TestRunJobQueueName" "story-test-pipeline"
```

### Step 6 — Configure the Worker

```bash
cd source/Testurio.Worker

dotnet user-secrets set "Infrastructure:CosmosConnectionString" "<primary connection string from https://localhost:8081/_explorer/index.html>"
dotnet user-secrets set "Infrastructure:CosmosDatabaseName" "testurio"
dotnet user-secrets set "Infrastructure:ServiceBusConnectionString" "<your-service-bus-connection-string>"
dotnet user-secrets set "Worker:TestRunJobQueueName" "story-test-pipeline"
```

### Step 7 — Run

Open two terminals from the repository root.

**Terminal 1 — API** (listens on `http://localhost:5225`):

```bash
dotnet run --project source/Testurio.Api/Testurio.Api.csproj
```

**Terminal 2 — Worker** (background service, no HTTP port):

```bash
dotnet run --project source/Testurio.Worker/Testurio.Worker.csproj
```

### Step 8 — Verify

```bash
curl http://localhost:5225/openapi/v1.json
```

A JSON OpenAPI document confirms the API started cleanly. A startup config error would crash the process before it could respond.

### Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| `ValidateOnStart` exception at launch | A required secret is missing | Re-run the `user-secrets set` commands for that project |
| Cosmos connection refused | Emulator not running | Start the Cosmos Emulator and wait for the green tray icon |
| SSL cert error on `https://localhost:7102` | Dev cert not trusted | Run `dotnet dev-certs https --trust` once |
| Service Bus auth error | Wrong connection string | Re-copy the Primary Connection String from the Azure portal |

> In production, all secrets are fetched from Azure Key Vault via Managed Identity. The `PassthroughSecretResolver` used in development returns config values as-is — no Key Vault needed locally.

## Tests

```bash
# All tests
dotnet test

# Unit tests only
dotnet test tests/Testurio.UnitTests/

# Integration tests only
dotnet test tests/Testurio.IntegrationTests/
```

## Infrastructure

Bicep templates live in `infra/modules/` (one file per Azure service). Kubernetes manifests for the vLLM GPU deployment are in `infra/k8s/`. Azure resources are provisioned once per environment — no per-tenant resources.

## Project layout

```
source/
├── Testurio.Web/            # Next.js — public site + user portal (planned)
├── Testurio.Api/            # ASP.NET Core — REST API + webhooks
├── Testurio.Worker/         # .NET Worker Service — test pipeline
├── Testurio.Core/           # Domain models, interfaces, value objects
├── Testurio.Plugins/        # Semantic Kernel plugins
└── Testurio.Infrastructure/ # Cosmos, Blob, Service Bus, Stripe, Key Vault
tests/
├── Testurio.UnitTests/
└── Testurio.IntegrationTests/
infra/
├── modules/                 # Bicep — one file per Azure service
└── k8s/                     # vLLM deployment + service manifests
documents/                   # Architecture and feature documentation
specifications/              # Per-feature specs, plans, and progress
```
