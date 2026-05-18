using Microsoft.Extensions.Logging;
using Testurio.Core.Interfaces;
using Testurio.Infrastructure;
using Testurio.Infrastructure.Cosmos;
using Testurio.Infrastructure.Seeding;
using Testurio.Worker;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddInfrastructure();
builder.Services.AddWorkerServices();

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddSingleton<ISecretResolver, PassthroughSecretResolver>();
}
else
{
    var keyVaultUri = builder.Configuration["KeyVault:Uri"]
        ?? throw new InvalidOperationException("KeyVault:Uri is required in non-Development environments.");
    builder.Services.AddSingleton<ISecretResolver>(_ => new KeyVaultSecretResolver(keyVaultUri));
}

var host = builder.Build();

var startupLogger = host.Services.GetRequiredService<ILogger<Program>>();

using var startupCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

try
{
    var initializer = host.Services.GetRequiredService<CosmosDbInitializer>();
    await initializer.InitializeAsync(startupCts.Token);
}
catch (Exception ex)
{
    startupLogger.LogCritical(ex, "Cosmos DB initialization failed. Worker cannot start.");
    throw;
}

try
{
    var seeder = host.Services.GetRequiredService<PromptTemplateSeeder>();
    await seeder.SeedAsync(startupCts.Token);
}
catch (Exception ex)
{
    startupLogger.LogCritical(ex, "Prompt template seeding failed. Worker cannot start.");
    throw;
}

try
{
    var planSeeder = host.Services.GetRequiredService<PlanSeeder>();
    await planSeeder.SeedAsync(startupCts.Token);
}
catch (Exception ex)
{
    // Plan data is an API-domain concern; a seeding failure should not block pipeline execution.
    startupLogger.LogWarning(ex, "Plan seeding failed at Worker startup — skipping. Plans will still be served by the API.");
}

host.Run();
