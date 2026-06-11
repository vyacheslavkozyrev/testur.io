using Microsoft.Extensions.Logging;
using Testurio.Core.Interfaces;
using Testurio.Infrastructure;
using Testurio.Infrastructure.Extensions;
using Testurio.Infrastructure.Cosmos;
using Testurio.Infrastructure.KeyVault;
using Testurio.Infrastructure.Seeding;
using Testurio.Worker;

DotEnv.Load();

var builder = Host.CreateApplicationBuilder(args);

// ── Secrets (Key Vault in production; local config in development) ────────────
// Must be called before AddInfrastructure() because factories depend on the singletons.
builder.Services.AddKeyVaultSecretLoader(builder.Configuration, builder.Environment);
await builder.Services.AddInfrastructureSecretsAsync(builder.Configuration, builder.Environment);
await builder.Services.AddAnthropicSecretsAsync(builder.Configuration, builder.Environment);
await builder.Services.AddAzureOpenAISecretsAsync(builder.Configuration, builder.Environment);

builder.Services.AddInfrastructure();
builder.Services.AddWorkerServices();

// ISecretResolver handles project-level credential secrets (Basic Auth, header tokens).
// In production it delegates to the already-registered IKeyVaultSecretLoader so we reuse
// the same SecretClient and retry logic rather than constructing a second one independently.
if (builder.Environment.IsTest())
{
    builder.Services.AddSingleton<ISecretResolver, PassthroughSecretResolver>();
}
else
{
    var keyVaultUri = builder.Configuration["KeyVault:Uri"]
        ?? throw new InvalidOperationException("KeyVault:Uri is required in non-Development environments.");
    builder.Services.AddSingleton<ISecretResolver>(sp =>
        new KeyVaultSecretResolver(sp.GetRequiredService<IKeyVaultSecretLoader>(), keyVaultUri));
}

var host = builder.Build();

var startupLogger = host.Services.GetRequiredService<ILogger<Program>>();

using var startupCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

try
{
    var initializer = host.Services.GetRequiredService<ICosmosDbInitializer>();
    await initializer.InitializeAsync(startupCts.Token);
}
catch (Exception ex)
{
    startupLogger.LogCritical(ex, "Cosmos DB initialization failed. Worker cannot start.");
    throw;
}

try
{
    var seeder = host.Services.GetRequiredService<IPromptTemplateSeeder>();
    await seeder.SeedAsync(startupCts.Token);
}
catch (Exception ex)
{
    startupLogger.LogCritical(ex, "Prompt template seeding failed. Worker cannot start.");
    throw;
}

try
{
    var planSeeder = host.Services.GetRequiredService<IPlanSeeder>();
    await planSeeder.SeedAsync(startupCts.Token);
}
catch (Exception ex)
{
    // Plan data is an API-domain concern; a seeding failure should not block pipeline execution.
    startupLogger.LogWarning(ex, "Plan seeding failed at Worker startup — skipping. Plans will still be served by the API.");
}

host.Run();
