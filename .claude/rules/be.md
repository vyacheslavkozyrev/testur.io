# Backend Rules — Testurio.Api / Testurio.Worker / Testurio.Core

## Implementation Layer Order
Never skip ahead — each layer depends on the previous:

| Tag | Scope |
|-----|-------|
| `[Migration]` | EF Core migration files |
| `[Domain]` | Entities, interfaces, value objects — `Testurio.Core` |
| `[Infra]` | Repositories, EF config, DI registration — `Testurio.Infrastructure` |
| `[App]` | DTOs, services, validators |
| `[API]` | Minimal API endpoints, middleware, route config — `Testurio.Api` |
| `[Config]` | App configuration, constants, feature flags |

## API Style
- Use **Minimal APIs** — no MVC controllers; group routes with `RouteGroupBuilder`
- Enable built-in .NET 10 validation on endpoint parameters (replaces FluentValidation middleware boilerplate)
- Return `TypedResults` for strong typing on all endpoints
- Version APIs via route prefix (`/v1/`, `/v2/`) using `Asp.Versioning.Http`

## Error Handling
- Register `AddProblemDetails()` + `IExceptionHandler` implementations — never write raw catch-and-return blocks in endpoints
- All unhandled exceptions produce RFC 9457 `ProblemDetails` responses; never leak stack traces or SQL in `Detail`
- `ValidationException` maps to `ValidationProblemDetails` automatically via the exception handler

## Dependency Injection
- Register all dependencies via DI — no service locator, no static accessors
- Use **keyed services** (`services.AddKeyedScoped<T>(key)`) when multiple implementations of an interface coexist (e.g., different storage backends)
- Validate `IOptions<T>` at startup with `services.AddOptions<T>().Bind(...).ValidateDataAnnotations().ValidateOnStart()`

## Async & Performance
- All I/O must be `async`/`await` — no `.Result` or `.Wait()` calls
- Use `CancellationToken` on every async method and forward it to EF Core, Cosmos, and HTTP calls
- Prefer **compiled EF Core queries** (`EF.CompileAsyncQuery`) for hot read paths
- Use `AsNoTracking()` for all read-only queries
- Use named query filters in EF Core for multi-tenant soft-delete scoping — never replicate `WHERE userId = @id` by hand
- Cache with `IHybridCache` (replaces `IMemoryCache` + `IDistributedCache` pattern) for hot lookup data

## Logging & Observability
- Use `ILogger<T>` injected via DI — never `LogManager.GetLogger` or static loggers
- Use **compile-time log source generation** (`[LoggerMessage]` attribute) for all high-frequency log calls
- Instrument with **OpenTelemetry** (traces + metrics + logs) — export to Azure Monitor via `UseAzureMonitor()`
- Include `traceId`/`spanId` in structured log output for distributed trace correlation
- Never log passwords, tokens, PII, or request bodies containing sensitive fields

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
- Scope every Cosmos DB query to `userId` — no cross-partition queries; use EF Core named query filters where applicable
- Never store credentials in Cosmos DB — persist only Key Vault secret references
- No hardcoded secrets — use Azure Key Vault + Managed Identity
- Load full project config in a single Cosmos read per worker job
- Enforce HTTPS, HSTS, and secure cookie settings at the middleware level

## Infrastructure
- All Azure resources defined in **Bicep** under `infra/modules/` — one file per service
- Kubernetes manifests for vLLM in `infra/k8s/`
- Never provision per-tenant Azure resources
- Use `MapStaticAssets()` instead of `UseStaticFiles()` for static file serving (build-time compression + SHA-256 ETags)
