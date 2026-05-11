---
name: Testurio — Architecture
version: 0.5.0
status: draft
updated: 2026-05-10
tags: [technical, architecture]
---

# Testurio — Architecture

## High-Level Pattern

Three-layer SaaS platform: a public website + user portal (frontend), a backend API serving both the portal and webhooks, and an async testing pipeline triggered by PM tool events.

```
                    ┌─────────────────────────────────────┐
                    │          Users / Browsers           │
                    └────────┬──────────────┬─────────────┘
                             │              │
               (public site) │              │ (portal / dashboard)
                             ▼              ▼
                    ┌─────────────────────────────────────┐
                    │     Azure Static Web Apps           │
                    │  Next.js / React                    │
                    │  - Marketing & pricing pages        │
                    │  - Plan purchase (payment widget)   │
                    │  - Personal area (settings)         │
                    │  - Project management               │
                    │  - Statistics & test results        │
                    └──────────────┬──────────────────────┘
                                   │ REST / JSON
                    ┌──────────────▼──────────────────────┐
                    │  ASP.NET Core Web API               │
                    │  (Azure App Service)                │
                    │                                     │
                    │  /api/account   — auth, settings    │
                    │  /api/projects  — CRUD, config      │
                    │  /api/stats     — results, history  │
                    │  /webhooks/ado  — ADO status change │
                    │  /webhooks/jira — Jira status change│
                    └───┬──────────────────────┬──────────┘
                        │                      │
              (accounts │ projects)    (webhook │ events)
                        ▼                      ▼
             ┌─────────────────┐    ┌──────────────────────┐
             │  Azure Cosmos DB│    │  Azure Service Bus   │
             │  - Users        │    │  durable job queue   │
             │  - Projects     │    └──────────┬───────────┘
             │  - Test results │               │
             └─────────────────┘    ┌──────────▼───────────────────────────┐
                                    │  .NET Worker Service                          │
                                    │  (Azure Container Apps)                       │
                                    │                                               │
                                    │  ┌──────────────────────────────────────────┐ │
                                    │  │ 1. StoryParser                           │ │
                                    │  │    template check → AI fallback → warn   │ │
                                    │  ├──────────────────────────────────────────┤ │
                                    │  │ 2. AgentRouter                           │ │
                                    │  │    resolve test_type → pick generators   │ │
                                    │  ├──────────────────────────────────────────┤ │
                                    │  │ 3. MemoryRetrieval                       │ │
                                    │  │    embed story → vector search → top-3   │ │
                                    │  ├──────────────────────────────────────────┤ │
                                    │  │ 4. Generators  [parallel]                │ │
                                    │  │    ApiTestGenerator + UiE2eTestGenerator  │ │
                                    │  ├──────────────────────────────────────────┤ │
                                    │  │ 5. ExecutorRouter                        │ │
                                    │  │    HttpExecutor (api)                    │ │
                                    │  │    PlaywrightExecutor (ui_e2e)           │ │
                                    │  ├──────────────────────────────────────────┤ │
                                    │  │ 6. ReportWriter                          │ │
                                    │  │    AI verdict → post to ADO / Jira       │ │
                                    │  ├──────────────────────────────────────────┤ │
                                    │  │ 7. FeedbackLoop                          │ │
                                    │  │    update passRate → soft-delete         │ │
                                    │  ├──────────────────────────────────────────┤ │
                                    │  │ 8. MemoryWriter                          │ │
                                    │  │    embed + upsert effective scenarios    │ │
                                    │  └──────────────────────────────────────────┘ │
                                    └──────────────────────────────┬────────────────┘
                                                                   │ HTTPS (Anthropic SDK)
                                           ┌───────────────────────▼────────────────┐
                                           │  Anthropic Claude API                  │
                                           │  Model: claude-opus-4-7                │
                                           │  Adaptive thinking enabled             │
                                           └────────────────────────────────────────┘
```

---

## Azure Services Map

| Purpose                      | Service                                  |
| ---------------------------- | ---------------------------------------- |
| Public website + user portal | Azure Static Web Apps (Next.js / React)  |
| API (portal + webhooks)      | Azure App Service — ASP.NET Core         |
| Authentication               | Azure AD B2C                             |
| Payments                     | Stripe (external) via API                |
| Message queue                | Azure Service Bus (Standard+)            |
| Worker / test pipeline       | Azure Container Apps                     |
| LLM inference                | Anthropic Claude API (`claude-opus-4-7`) |
| Data storage                 | Azure Cosmos DB                          |
| Screenshots / test artifacts | Azure Blob Storage                       |
| Secrets                      | Azure Key Vault + Managed Identity       |
| Worker egress / static IPs   | Azure NAT Gateway (fixed egress IPs)     |
| Webhook auth / rate limiting | Azure API Management                     |
| Memory / vector search       | Azure AI Search (vector index)           |
| Observability                | Azure Application Insights               |
| Container registry           | Azure Container Registry                 |
| CDN / edge                   | Azure Front Door                         |

---

## Frontend (Public Website + User Portal)

Single Next.js application hosted on Azure Static Web Apps, split into two logical areas:

### Public Area (unauthenticated)

- Landing / marketing pages
- Pricing and plan comparison
- Sign-up and login (Azure AD B2C hosted UI)

### Personal Area (authenticated)

- **Dashboard** — overview of all projects and recent test runs
- **Account Settings** — appearance, language, personal information, payment method
- **Project Management** — create/edit projects; configure URL, testing strategy, model settings, custom prompts, PM tool tokens, environment access settings
- **Statistics** — per-project test history, pass/fail trends, individual test run reports

---

## Backend API Responsibilities

| Route group      | Responsibility                                      |
| ---------------- | --------------------------------------------------- |
| `/api/account`   | Profile, preferences, subscription status           |
| `/api/billing`   | Plan purchase, payment method (delegates to Stripe) |
| `/api/projects`  | CRUD for projects and their configuration           |
| `/api/stats`     | Read test run history and results from Cosmos       |
| `/webhooks/ado`  | Receive Azure DevOps status-change events           |
| `/webhooks/jira` | Receive Jira status-change events                   |

---

## Multi-Tenancy Model

Testurio uses **logical multi-tenancy** — all clients share a single infrastructure stack. Isolation is enforced at two layers:

| Layer        | Mechanism                                                                                                                                               |
| ------------ | ------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **API**      | Every request carries an Azure AD B2C JWT; the API extracts `userId` from the token and scopes all queries to that user's data                          |
| **Database** | `userId` is the partition key on all Cosmos DB containers (Users, Projects, TestResults); cross-partition queries that could leak data are never issued |

No client-visible tenant ID is required — the authenticated identity is the tenant. A new client account is simply a new user record; no infrastructure is provisioned per client.

---

## Testing Pipeline (async)

Webhook received → Service Bus → Worker dequeues → 8-stage pipeline:

1. **StoryParser** (`Testurio.Pipeline.StoryParser`)
   Detects whether the raw story matches the Testurio template. If yes: parses directly into structured JSON (title, description, acceptance_criteria, entities, actions, edge_cases). If no: calls Claude to convert it, posts a warning comment to the originating ADO/Jira ticket, then continues with the converted story.

2. **AgentRouter** (`Testurio.Pipeline.AgentRouter`)
   Reads the project `test_types` config (`api | ui_e2e | both`), resolves which generator agents to invoke, and coordinates their parallel execution.

3. **MemoryRetrieval** (`Testurio.Pipeline.MemoryRetrieval`)
   Embeds the parsed story text via Azure OpenAI `text-embedding-3-small`, then runs a Cosmos DiskANN vector search scoped to `userId + testType`. Returns the top-3 most semantically similar past scenarios per enabled test type.

4. **Generators — parallel** (`Testurio.Pipeline.Generators`)
   MVP: `ApiTestGeneratorAgent` and `UiE2eTestGeneratorAgent` run in parallel. Each receives the parsed story, top-3 memory examples, and project config; calls Claude API with adaptive thinking; outputs a typed scenario JSON array.

5. **ExecutorRouter** (`Testurio.Pipeline.Executors`)
   Routes generated scenarios to the correct executor:
   - `HttpExecutor` — API scenarios: sends HTTP requests, validates status codes, JSON paths, and headers
   - `PlaywrightExecutor` — UI E2E scenarios: browser automation, captures screenshots at each step

6. **ReportWriter** (`Testurio.Pipeline.ReportWriter`)
   Claude writes a structured verdict report (PASSED / FAILED), listing each scenario with result, duration, and failure diffs. Posts it as a comment to the originating ADO/Jira ticket and writes a `TestResult` record to Cosmos.

7. **FeedbackLoop** (`Testurio.Pipeline.FeedbackLoop`)
   Updates `passRate` on any memory entries that were reused in this run (weighted average up on pass, down on fail). Triggers soft-delete (`isDeleted: true`) when `passRate < 0.5` after `runCount >= 5`.

8. **MemoryWriter** (`Testurio.Pipeline.MemoryWriter`)
   For all-pass runs: generates the story embedding and upserts the scenario to the Cosmos `TestMemory` container. Supports cross-project opt-in (anonymized `userId` via SHA-256 hash, `projectId: null`).

---

## Testing Environment Access

The Test Executor must reach the client's product URL, which is typically a protected staging or preview environment. Two mechanisms are supported per project; the QA lead chooses one in project settings.

### Option A — IP Allowlisting (recommended)

All worker containers egress through a single Azure NAT Gateway, which provides a small, fixed set of public IPs. Testurio publishes these IPs in its documentation. The client adds them to their firewall, CDN allowlist (e.g. Cloudflare, Azure Front Door), or network security group — a one-time change requiring no credentials to be shared.

```
Container Apps workers
        │
        ▼
  Azure NAT Gateway  ──►  fixed public IPs  ──►  client firewall allowlist
        │
        ▼
  client product_url (staging / preview)
```

**Client setup (one time):**

1. Copy the Testurio published egress IP range from the documentation.
2. Add the IPs to your staging environment's firewall or CDN allowlist.
3. No further action needed — the worker connects transparently on every test run.

### Option B — Credential-Based Access (fallback)

For environments where firewall rules cannot be modified, the project stores credentials encrypted in Azure Key Vault. Two sub-options:

| Sub-option              | How it works                                                                  | Playwright behaviour                                                    |
| ----------------------- | ----------------------------------------------------------------------------- | ----------------------------------------------------------------------- |
| **HTTP Basic Auth**     | Username + password stored per project; URL is `https://user:pass@host/...`   | Playwright passes credentials via `httpCredentials` option              |
| **Custom header token** | A shared secret header (e.g. `X-Testurio-Token`) checked by client middleware | Playwright injects the header into every request via `extraHTTPHeaders` |

**Client setup — Basic Auth:**

1. Protect your staging environment with HTTP Basic Auth (e.g. nginx, middleware).
2. Enter the username and password in Testurio project settings.

**Client setup — Custom header token:**

1. Add middleware to your staging environment that rejects requests missing `X-Testurio-Token: <secret>`.
2. Generate a secret and enter it in Testurio project settings.

### Project Config Fields

| Field                | Description                                                                                      |
| -------------------- | ------------------------------------------------------------------------------------------------ |
| `test_type`          | `api` \| `ui_e2e` \| `both` (MVP); extended to `smoke \| a11y \| visual \| performance` post-MVP |
| `access_mode`        | `ip_allowlist` \| `basic_auth` \| `header_token`                                                 |
| `basic_auth_user`    | Username (Basic Auth mode only); stored in Key Vault                                             |
| `basic_auth_pass`    | Password (Basic Auth mode only); stored in Key Vault                                             |
| `header_token_name`  | Header name (header token mode only), e.g. `X-Testurio-Token`                                    |
| `header_token_value` | Header value (header token mode only); stored in Key Vault                                       |

Credentials are never stored in Cosmos DB directly — only a Key Vault secret reference is persisted in the project document.

---

## LLM Component Detail

### Model

- **Provider**: Anthropic Claude API
- **Model**: `claude-opus-4-7`
- **Thinking**: adaptive thinking enabled on every call

### SDK Integration

```csharp
// Registration (Testurio.Worker DI setup)
services.AddSingleton<AnthropicClient>(_ =>
    new AnthropicClient { ApiKey = config["Anthropic:ApiKey"] });

// Usage inside a pipeline stage
var response = await _client.Messages.Create(new MessageCreateParams
{
    Model     = Model.ClaudeOpus4_7,
    MaxTokens = 16000,
    Thinking  = new ThinkingConfigAdaptive(),
    Messages  = [new() { Role = Role.User, Content = prompt }],
}, ct);
```

- API key stored in Azure Key Vault; loaded at startup via Managed Identity
- No self-hosted GPU infrastructure required

---

## Project Structure

```
testur.io/
├── source/
│   ├── Testurio.Web/                        # Next.js — public site + user portal
│   ├── Testurio.Api/                        # ASP.NET Core — portal API + webhooks
│   ├── Testurio.Worker/                     # .NET Worker Service — orchestrator only, no pipeline logic
│   ├── Testurio.Core/                       # Domain models, interfaces for all pipeline stages
│   ├── Testurio.Infrastructure/             # Cosmos, Blob, Service Bus, Stripe, embedding client
│   │
│   ├── Testurio.Pipeline.StoryParser/       # Stage 1 — template detection, AI fallback, PM warning
│   ├── Testurio.Pipeline.AgentRouter/       # Stage 2 — resolve test_type, coordinate parallel generators
│   ├── Testurio.Pipeline.MemoryRetrieval/   # Stage 3 — embed story, vector search, return top-k examples
│   ├── Testurio.Pipeline.Generators/        # Stage 4 — ApiTestGeneratorAgent, UiE2eTestGeneratorAgent
│   ├── Testurio.Pipeline.Executors/         # Stage 5 — HttpExecutor, PlaywrightExecutor
│   ├── Testurio.Pipeline.ReportWriter/      # Stage 6 — AI verdict, ADO / Jira post-back
│   ├── Testurio.Pipeline.FeedbackLoop/      # Stage 7 — passRate updates, soft-delete
│   └── Testurio.Pipeline.MemoryWriter/      # Stage 8 — embed + upsert effective scenarios
├── tests/
│   ├── Testurio.UnitTests/
│   └── Testurio.IntegrationTests/
└── infra/
    ├── main.bicep
    └── modules/
        ├── staticwebapp.bicep
        ├── appservice.bicep
        ├── servicebus.bicep
        ├── cosmos.bicep
        ├── adb2c.bicep
        └── apim.bicep
```

Each `Testurio.Pipeline.*` project exposes a single interface defined in `Testurio.Core`. `Testurio.Worker` depends only on those interfaces; concrete implementations are registered via DI at startup.

---

## Memory Architecture

### Cosmos DB Container: `TestMemory`

Partition key: `userId`. Stores effective test scenarios with semantic embeddings used as few-shot examples in future generation calls.

| Field            | Description                                             |
| ---------------- | ------------------------------------------------------- |
| `id`             | UUID v4                                                 |
| `userId`         | B2C OID — partition key                                 |
| `projectId`      | Project UUID, or `null` for cross-project shared memory |
| `testType`       | `api \| ui_e2e` (MVP); extended post-MVP                |
| `storyEmbedding` | `float32[1536]` — Azure OpenAI `text-embedding-3-small` |
| `storyText`      | Original parsed story text used for similarity search   |
| `scenarioText`   | Serialized scenario JSON                                |
| `passRate`       | 0.0–1.0 — quality signal updated on each reuse          |
| `runCount`       | Number of times this scenario has been reused           |
| `lastUsedAt`     | ISO 8601 timestamp                                      |
| `isDeleted`      | Soft-delete flag                                        |

**Vector index (DiskANN):** path `/storyEmbedding`, cosine distance, 1536 dimensions.

### Memory Quality Loop

| Event                                  | Action                                                |
| -------------------------------------- | ----------------------------------------------------- |
| All assertions pass                    | `store_memory` called; `passRate = 1.0`, `runCount++` |
| Scenario reused and passes             | `passRate` weighted average updated upward            |
| Scenario reused and fails              | `passRate` decremented                                |
| `passRate < 0.5` after `runCount >= 5` | `isDeleted: true`                                     |
| Cross-project opt-in                   | `projectId: null`, `userId` SHA-256 hashed            |

### Embedding Service

`IEmbeddingService` is registered in `Testurio.Infrastructure` and injected into both `MemoryRetrieval` and `MemoryWriter`. Calls Azure OpenAI `text-embedding-3-small` (1536 dimensions, cheap, no additional infrastructure).

---

## Key Design Decisions

**Azure AD B2C** handles authentication — supports social login, email/password, and MFA out of the box. Avoids building custom auth.

**Static Web Apps + App Service** — frontend and API are deployed independently. The portal calls the API via a standard REST boundary; the same API also handles webhooks.

**Stripe** for billing — plan purchase and payment method management delegate entirely to Stripe Checkout and the Customer Portal, keeping PCI scope minimal.

**Cosmos DB** stores users, projects, and test results in a single account with separate containers. The per-project document includes all configuration, making it easy to load everything a worker job needs in one read.

**Claude API over self-hosted LLM** — no GPU infrastructure to provision, scale, or maintain. Anthropic manages availability and model updates; the worker simply calls the API. Adaptive thinking is enabled on every generation call for higher-quality test scenarios.

**Logical multi-tenancy over physical isolation** — all clients share a single Cosmos DB account. Tenant isolation is enforced by `userId` as the partition key on every container, combined with API-layer auth (Azure AD B2C token validation on every request). No client can access another's data. Physical per-tenant accounts (one Cosmos account per client) are explicitly out of scope for v1 — they would multiply operational overhead linearly with client count and are only justified for enterprise compliance requirements.

**Global memory layer via Azure AI Search** — past test scenarios and outcomes are embedded and indexed after every run. The `MemoryRetriever` plugin retrieves semantically similar examples before TestGenerator runs, injecting them as few-shot context. Retrieval is always scoped to `userId`, preventing cross-tenant leakage. Indexing is wired from v1; retrieval activates per-project once sufficient signal accumulates (~hundreds of runs).

**NAT Gateway for static egress IPs** — all worker outbound traffic routes through a single NAT Gateway, giving Testurio a predictable, publishable IP range. This makes IP allowlisting a reliable, zero-credential option for clients. Credentials (Basic Auth, header tokens) are stored exclusively in Key Vault; only a secret reference lives in the project document in Cosmos DB.

---

## Non-Goals (v1)

- Mobile app testing
- Test case version history
- Team / multi-user accounts
