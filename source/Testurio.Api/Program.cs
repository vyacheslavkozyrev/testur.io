using Testurio.Api.Clients;
using Testurio.Api.Controllers;
using Testurio.Api.Services;
using Testurio.Core.Interfaces;
using Testurio.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddInfrastructure();
builder.Services.AddHttpClient();
builder.Services.AddSingleton<IJiraApiClient>(sp =>
    new JiraApiClient(sp.GetRequiredService<IHttpClientFactory>().CreateClient(), sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<JiraApiClient>>()));
builder.Services.AddScoped<JiraWebhookService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/webhooks"))
        context.Request.EnableBuffering();
    await next(context);
});

app.MapJiraWebhooks();

app.Run();

public partial class Program { }
