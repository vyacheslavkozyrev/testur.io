using Testurio.Api.Clients;
using Testurio.Api.Controllers;
using Testurio.Api.Middleware;
using Testurio.Api.Services;
using Testurio.Core.Interfaces;
using Testurio.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddAuthentication();
builder.Services.AddAuthorization();
builder.Services.AddInfrastructure();
builder.Services.AddHttpClient<IJiraApiClient, JiraApiClient>();
builder.Services.AddScoped<IJiraWebhookService, JiraWebhookService>();
builder.Services.AddScoped<JiraWebhookSignatureFilter>();

var app = builder.Build();

// EnableBuffering must run before the request body is consumed — register it first.
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/v1/webhooks"))
        context.Request.EnableBuffering();
    await next(context);
});

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseExceptionHandler();
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapJiraWebhooks();

app.Run();

public partial class Program { }
