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
No MVC controllers. Group routes with `RouteGroupBuilder`, return `TypedResults`, version via route prefix:

```csharp
var v1 = app.MapGroup("/v1").RequireAuthorization();

var projects = v1.MapGroup("/projects");
projects.MapGet("/",    async (IProjectService svc, CancellationToken ct) =>
    TypedResults.Ok(await svc.ListAsync(ct)));
projects.MapGet("/{id:guid}", async (Guid id, IProjectService svc, CancellationToken ct) =>
    await svc.GetAsync(id, ct) is { } p ? TypedResults.Ok(p) : TypedResults.NotFound());
projects.MapPost("/",   async ([FromBody] CreateProjectRequest req, IProjectService svc, CancellationToken ct) =>
{
    var project = await svc.CreateAsync(req, ct);
    return TypedResults.Created($"/v1/projects/{project.Id}", project);
});
```

Built-in .NET 10 parameter validation fires automatically — no FluentValidation middleware needed.

## Error Handling
Register `AddProblemDetails()` and a central `IExceptionHandler` — never catch-and-return in endpoints:

```csharp
// Program.cs
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

app.UseExceptionHandler();
```

```csharp
// GlobalExceptionHandler.cs
internal sealed class GlobalExceptionHandler(IProblemDetailsService pds)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext ctx, Exception ex, CancellationToken ct)
    {
        var (status, title) = ex switch
        {
            ValidationException v => (400, v.Message),
            NotFoundException    => (404, "Resource not found"),
            _                    => (500, "An unexpected error occurred"),
        };
        ctx.Response.StatusCode = status;
        return await pds.TryWriteAsync(new() { HttpContext = ctx,
            ProblemDetails = { Status = status, Title = title } });
    }
}
```

Never include stack traces, SQL, or internal ids in `ProblemDetails.Detail`.

## Dependency Injection
Use keyed services when multiple implementations of the same interface coexist. Validate options at startup:

```csharp
// Keyed services
services.AddKeyedScoped<IStorageClient, CosmosStorageClient>("cosmos");
services.AddKeyedScoped<IStorageClient, BlobStorageClient>("blob");

// Inject by key
public ProjectRepository([FromKeyedServices("cosmos")] IStorageClient storage) { }

// Options validation — fails fast on bad config before first request
services.AddOptions<CosmosOptions>()
    .Bind(configuration.GetSection("Cosmos"))
    .ValidateDataAnnotations()
    .ValidateOnStart();
```

## Async & Performance
All I/O is `async`/`await`. Pass and forward `CancellationToken` everywhere. Use compiled queries on hot read paths:

```csharp
// Compiled query — compiled once, reused on every call
private static readonly Func<AppDbContext, Guid, IAsyncEnumerable<Project>> GetByUser =
    EF.CompileAsyncQuery((AppDbContext db, Guid userId) =>
        db.Projects
          .AsNoTracking()
          .Where(p => p.UserId == userId)
          .OrderByDescending(p => p.CreatedAt));

public async Task<List<Project>> ListAsync(Guid userId, CancellationToken ct) =>
    await GetByUser(_db, userId).ToListAsync(ct);
```

```csharp
// IHybridCache for hot lookups (replaces IMemoryCache + IDistributedCache)
public async Task<ProjectConfig> GetConfigAsync(Guid projectId, CancellationToken ct) =>
    await _cache.GetOrCreateAsync(
        $"project:{projectId}",
        async cct => await _db.Projects.AsNoTracking()
                               .SingleAsync(p => p.Id == projectId, cct),
        cancellationToken: ct);
```

Named EF Core query filters enforce `userId` scoping globally — never repeat `Where(p => p.UserId == userId)` by hand:

```csharp
// AppDbContext.OnModelCreating
builder.Entity<Project>().HasQueryFilter(p => p.UserId == _currentUserId);
```

## Logging & Observability
Use compile-time `[LoggerMessage]` source generation for high-frequency calls. Inject `ILogger<T>` — never static loggers:

```csharp
public partial class TestRunService(ILogger<TestRunService> logger)
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Test run {RunId} started for project {ProjectId}")]
    private partial void LogRunStarted(Guid runId, Guid projectId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Test run {RunId} failed after {ElapsedMs}ms")]
    private partial void LogRunFailed(Guid runId, long elapsedMs);
}
```

```csharp
// Program.cs — OpenTelemetry export to Azure Monitor
builder.Services.AddOpenTelemetry()
    .WithTracing(t => t.AddAspNetCoreInstrumentation().AddEntityFrameworkCoreInstrumentation())
    .WithMetrics(m => m.AddAspNetCoreInstrumentation())
    .UseAzureMonitor();
```

Never log passwords, tokens, PII, or request bodies that contain sensitive fields.

## LLM / Claude API
All LLM calls use the **Anthropic C# SDK** (`dotnet add package Anthropic`). API key loaded from Key Vault via `IConfiguration`:

```csharp
// Registration
services.AddSingleton<AnthropicClient>(_ =>
    new AnthropicClient { ApiKey = configuration["Anthropic:ApiKey"] });
```

Every call enables adaptive thinking. Unwrap content blocks with `TryPick*`:

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

For long outputs (test generation), stream and collect:

```csharp
await foreach (var evt in _client.Messages.CreateStreaming(new MessageCreateParams
{
    Model     = Model.ClaudeOpus4_7,
    MaxTokens = 64000,
    Thinking  = new ThinkingConfigAdaptive(),
    Messages  = [new() { Role = Role.User, Content = prompt }],
}))
{
    if (evt.TryPickContentBlockDelta(out var delta) &&
        delta.Delta.TryPickText(out var chunk))
        sb.Append(chunk.Text);
}
```

Worker pipeline stages are discrete classes — do not merge: `StoryParser` → `TestGenerator` → `TestExecutor` → `ReportWriter`. Never call the Anthropic API from `Testurio.Api`.

## Security & Multi-Tenancy
Extract `userId` from the Azure AD B2C JWT before any data access. Use a middleware that sets it on a scoped service:

```csharp
// ClaimsPrincipalExtensions
public static Guid GetUserId(this ClaimsPrincipal user) =>
    Guid.Parse(user.FindFirstValue("oid") ?? throw new UnauthorizedAccessException());

// Endpoint
projects.MapGet("/", async (ClaimsPrincipal user, IProjectService svc, CancellationToken ct) =>
    TypedResults.Ok(await svc.ListAsync(user.GetUserId(), ct)));
```

Cosmos DB partition key is `userId` — every query is scoped by it, making cross-tenant reads impossible:

```csharp
var container = _cosmosClient.GetContainer("testurio", "Projects");
var query = new QueryDefinition("SELECT * FROM c WHERE c.userId = @userId")
    .WithParameter("@userId", userId.ToString());
// PartitionKey enforces isolation at the SDK level
var iterator = container.GetItemQueryIterator<ProjectDocument>(
    query, requestOptions: new() { PartitionKey = new PartitionKey(userId.ToString()) });
```

Credentials (Basic Auth, header tokens) are stored in Key Vault — only the secret URI lives in Cosmos:

```csharp
// Stored in ProjectDocument
public string? BasicAuthSecretUri { get; init; }  // Key Vault URI, never the value

// Retrieved at worker runtime
var secret = await _secretClient.GetSecretAsync(project.BasicAuthSecretUri, ct: ct);
```

Enforce HTTPS, HSTS, and secure cookies at middleware level — not per-endpoint.

## Infrastructure
All Azure resources defined in Bicep under `infra/modules/` — one file per service. Never provision per-tenant resources.

Use `MapStaticAssets()` — not `UseStaticFiles()` — for build-time compression and SHA-256 ETags:

```csharp
app.MapStaticAssets();  // not app.UseStaticFiles()
```
