using Testurio.Core.Interfaces;
using Testurio.Infrastructure;
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
    builder.Services.AddSingleton<ISecretResolver, KeyVaultSecretResolver>();
}

var host = builder.Build();

// Feature 0028: seed initial PromptTemplate documents before the worker starts processing messages.
// The seeder is idempotent — it skips documents that already exist so manual edits are preserved.
using (var scope = host.Services.CreateScope())
{
    var seeder = scope.ServiceProvider.GetRequiredService<PromptTemplateSeeder>();
    await seeder.SeedAsync();
}

host.Run();
