using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Testurio.Api.Clients;
using Testurio.Api.Controllers;
using Testurio.Api.Middleware;
using Testurio.Api.Services;
using Testurio.Core.Interfaces;
using Testurio.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOptions<AzureAdB2COptions>()
    .BindConfiguration("AzureAdB2C")
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer();
// Bind JWT Bearer options from the already-validated AzureAdB2COptions so a missing config key
// fails at startup (via ValidateOnStart above) rather than silently producing null Authority/Audience.
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<AzureAdB2COptions>>((jwtOpts, b2cOpts) =>
    {
        jwtOpts.Authority = b2cOpts.Value.Authority;
        jwtOpts.Audience = b2cOpts.Value.ClientId;
    });
builder.Services.AddAuthorization();
builder.Services.AddInfrastructure();
builder.Services.AddHttpClient<IJiraApiClient, JiraApiClient>();
builder.Services.AddScoped<IJiraWebhookService, JiraWebhookService>();
builder.Services.AddSingleton<JiraWebhookSignatureFilter>();
builder.Services.AddTransient<RequestBodyBufferingMiddleware>();

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddSingleton<ISecretResolver, PassthroughSecretResolver>();
}
else
{
    builder.Services.AddSingleton<ISecretResolver, KeyVaultSecretResolver>();
}

var app = builder.Build();

// EnableBuffering must run before the request body is consumed — register it first.
app.UseMiddleware<RequestBodyBufferingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseExceptionHandler();
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseAuthentication();
app.UseAuthorization();

app.MapJiraWebhooks();

app.Run();

public sealed class AzureAdB2COptions
{
    [Required] public required string Authority { get; init; }
    [Required] public required string ClientId { get; init; }
}

public partial class Program { }
