using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authentication.JwtBearer;
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
builder.Services.AddAuthentication()
    .AddJwtBearer(opts =>
    {
        opts.Authority = builder.Configuration["AzureAdB2C:Authority"];
        opts.Audience = builder.Configuration["AzureAdB2C:ClientId"];
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

var app = builder.Build();

// EnableBuffering must run before the request body is consumed — register it first.
app.UseMiddleware<RequestBodyBufferingMiddleware>();

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

public sealed class AzureAdB2COptions
{
    [Required] public required string Authority { get; init; }
    [Required] public required string ClientId { get; init; }
}

public partial class Program { }
