# Backend Rules — Testurio.Api / Testurio.Worker / Testurio.Core

## Implementation Layer Order
Never skip ahead — each layer depends on the previous:

| Tag | Scope |
|-----|-------|
| `[Migration]` | EF Core migration files |
| `[Domain]` | Entities, interfaces, value objects — `Testurio.Core` |
| `[Infra]` | Repositories, EF config, DI registration — `Testurio.Infrastructure` |
| `[App]` | DTOs, services, validators |
| `[API]` | Controllers, middleware, route config — `Testurio.Api` |
| `[Config]` | App configuration, constants, feature flags |

## Dependency Injection
- Register all dependencies via DI — no service locator, no static accessors

## LLM / Semantic Kernel
- All LLM calls go through **Semantic Kernel** — never call vLLM directly
- Register using the OpenAI-compatible endpoint:
  ```csharp
  builder.AddOpenAIChatCompletion(
      modelId: "llama-3.1-8b-testcases",
      endpoint: new Uri("http://vllm-service.llm.svc.cluster.local/v1"),
      apiKey: "internal-token"
  );
  ```
- Worker pipeline stages are discrete SK plugins — do not merge: `StoryParser` → `TestGenerator` → `TestExecutor` → `ReportWriter`

## Security & Multi-Tenancy
- Every API endpoint validates the Azure AD B2C JWT and extracts `userId` before any data access
- Scope every Cosmos DB query to `userId` — no cross-partition queries
- Never store credentials in Cosmos DB — persist only Key Vault secret references
- No hardcoded secrets — use Azure Key Vault + Managed Identity
- Load full project config in a single Cosmos read per worker job

## Infrastructure
- All Azure resources defined in **Bicep** under `infra/modules/` — one file per service
- Kubernetes manifests for vLLM in `infra/k8s/`
- Never provision per-tenant Azure resources
