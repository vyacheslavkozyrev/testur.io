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

var host = builder.Build();
host.Run();
