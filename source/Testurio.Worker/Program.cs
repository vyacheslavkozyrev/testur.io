using Testurio.Core.Interfaces;
using Testurio.Infrastructure;
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
host.Run();
