using Testurio.Api.Clients;
using Testurio.Api.Controllers;
using Testurio.Api.Services;
using Testurio.Core.Interfaces;
using Testurio.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddInfrastructure();
builder.Services.AddHttpClient<IJiraApiClient, JiraApiClient>();
builder.Services.AddScoped<IJiraWebhookService, JiraWebhookService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseExceptionHandler();
app.UseHttpsRedirection();

app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/v1/webhooks"))
        context.Request.EnableBuffering();
    await next(context);
});

app.MapJiraWebhooks();

app.Run();

public partial class Program { }
