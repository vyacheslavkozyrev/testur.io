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
- Use **keyed services** when multiple implementations of an interface coexist:
  ```csharp
  services.AddKeyedScoped<IStorageClient, CosmosClient>("cosmos");
  services.AddKeyedScoped<IStorageClient, BlobClient>("blob");
  ```
- Validate `IOptions<T>` at startup:
  ```csharp
  services.AddOptions<CosmosOptions>()
      .Bind(config.GetSection("Cosmos"))
      .ValidateDataAnnotations()
      .ValidateOnStart();
  ```

## Async & Performance
- All I/O must be `async`/`await` — no `.Result` or `.Wait()` calls
- Use `CancellationToken` on every async method and forward it to EF Core, Cosmos, and HTTP calls
- Prefer **compiled EF Core queries** (`EF.CompileAsyncQuery`) for hot read paths
- Use `AsNoTracking()` for all read-only queries
- Use named query filters in EF Core for multi-tenant soft-delete scoping — never replicate `WHERE userId = @id` by hand
- Cache with `IHybridCache` (replaces `IMemoryCache` + `IDistributedCache` pattern) for hot lookup data

## Logging & Observability
- Use `ILogger<T>` injected via DI — never `LogManager.GetLogger` or static loggers
- Use compile-time source generation for high-frequency log calls:
  ```csharp
  [LoggerMessage(Level = LogLevel.Information, Message = "Test run {RunId} started")]
  static partial void LogTestRunStarted(ILogger logger, Guid runId);
  ```
- Instrument with OpenTelemetry — export to Azure Monitor via `UseAzureMonitor()`
- Never log passwords, tokens, PII, or request bodies containing sensitive fields

## LLM / Claude API
- All LLM calls use the **Anthropic C# SDK** — `dotnet add package Anthropic`
- Register as a singleton; key is loaded from Key Vault via `IConfiguration`:
  ```csharp
  services.AddSingleton<AnthropicClient>(_ =>
      new AnthropicClient { ApiKey = config["Anthropic:ApiKey"] });
  ```
- Every LLM call uses adaptive thinking and streaming for long outputs:
  ```csharp
  using Anthropic;
  using Anthropic.Models.Messages;

  var response = await _client.Messages.Create(new MessageCreateParams
  {
      Model     = Model.ClaudeOpus4_7,
      MaxTokens = 16000,
      Thinking  = new ThinkingConfigAdaptive(),
      Messages  = [new() { Role = Role.User, Content = prompt }],
  }, ct);

  foreach (var block in response.Content)
  {
      if (block.TryPickText(out TextBlock? text))
          result = text.Text;
  }
  ```
- Worker pipeline stages are discrete classes — do not merge: `StoryParser` → `TestGenerator` → `TestExecutor` → `ReportWriter`
- Never call the Anthropic API from `Testurio.Api` — LLM work belongs in `Testurio.Worker` only

## Security & Multi-Tenancy
- Every API endpoint validates the Azure AD B2C JWT and extracts `userId` before any data access
- Scope every Cosmos DB query to `userId` — no cross-partition queries; use EF Core named query filters where applicable
- Never store credentials in Cosmos DB — persist only Key Vault secret references
- No hardcoded secrets — use Azure Key Vault + Managed Identity
- Load full project config in a single Cosmos read per worker job
- Enforce HTTPS, HSTS, and secure cookie settings at the middleware level

## Infrastructure
- All Azure resources defined in **Bicep** under `infra/modules/` — one file per service
- Never provision per-tenant Azure resources
- Use `MapStaticAssets()` instead of `UseStaticFiles()` for static file serving (build-time compression + SHA-256 ETags)
