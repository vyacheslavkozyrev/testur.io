using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Testurio.Api.Controllers;
using Testurio.Api.Endpoints;
using Testurio.Api.Middleware;
using Testurio.Api.Services;
using Testurio.Core.Interfaces;
using Testurio.Infrastructure;
using Testurio.Infrastructure.Blob;
using Testurio.Infrastructure.Security;
using Testurio.Infrastructure.Anthropic;

var builder = WebApplication.CreateBuilder(args);

var b2cOptions = builder.Services.AddOptions<AzureAdB2COptions>()
    .BindConfiguration("AzureAdB2C")
    .ValidateDataAnnotations();
if (!builder.Environment.IsDevelopment())
    b2cOptions.ValidateOnStart();

builder.Services.AddOpenApi();
builder.Services.ConfigureHttpJsonOptions(opts =>
{
    opts.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    opts.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
});
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<Testurio.Api.Middleware.GlobalExceptionHandler>();
builder.Services.AddHttpLogging(o =>
{
    o.LoggingFields = Microsoft.AspNetCore.HttpLogging.HttpLoggingFields.RequestMethod
        | Microsoft.AspNetCore.HttpLogging.HttpLoggingFields.RequestPath
        | Microsoft.AspNetCore.HttpLogging.HttpLoggingFields.ResponseStatusCode
        | Microsoft.AspNetCore.HttpLogging.HttpLoggingFields.Duration;
});
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddAuthentication(DevAuthHandler.SchemeName)
        .AddScheme<AuthenticationSchemeOptions, DevAuthHandler>(DevAuthHandler.SchemeName, _ => { });
}
else
{
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
}
builder.Services.AddAuthorization();
builder.Services.AddCors(options =>
{
    options.AddPolicy("DevPortal", policy =>
        policy.WithOrigins("http://localhost:3000")
              .AllowAnyHeader()
              .AllowAnyMethod());
});
builder.Services.AddInfrastructure();

// ILlmGenerationClient — used by PromptCheckService for AI-assisted prompt quality checks.
// The API key is optional at startup; if absent the prompt-check endpoint will fail at runtime
// (acceptable: the key is always present in non-development environments).
builder.Services.AddHttpClient<ILlmGenerationClient, AnthropicGenerationClient>((sp, client) =>
{
    var apiKey = builder.Configuration["Claude:ApiKey"] ?? string.Empty;
    if (!string.IsNullOrEmpty(apiKey))
        client.DefaultRequestHeaders.Add("x-api-key", apiKey);
})
.AddTypedClient<ILlmGenerationClient>((client, sp) =>
{
    var modelId = builder.Configuration["Claude:ModelId"] ?? "claude-opus-4-7";
    var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<AnthropicGenerationClient>>();
    return new AnthropicGenerationClient(client, modelId, logger);
});

builder.Services.AddScoped<IWorkItemTypeFilterService, WorkItemTypeFilterService>();
builder.Services.AddScoped<IJiraWebhookService, JiraWebhookService>();
builder.Services.AddScoped<IADOWebhookService, ADOWebhookService>();
builder.Services.AddScoped<IProjectService, ProjectService>();
builder.Services.AddScoped<IPMToolConnectionService, PMToolConnectionService>();
builder.Services.AddScoped<IPromptCheckService, PromptCheckService>();
builder.Services.AddScoped<IReportTemplateService, ReportTemplateService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddSingleton<JiraWebhookSignatureFilter>();
builder.Services.AddTransient<RequestBodyBufferingMiddleware>();
builder.Services.AddOptions<PMToolConnectionServiceOptions>()
    .BindConfiguration("PMTool")
    .Configure(opts =>
    {
        // Default to the public API base URL; overridden in appsettings.
        if (string.IsNullOrWhiteSpace(opts.ApiBaseUrl))
            opts.ApiBaseUrl = "https://api.testur.io";
    });

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
app.UseHttpLogging();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseExceptionHandler();
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
if (app.Environment.IsDevelopment())
{
    app.UseCors("DevPortal");
}
app.UseAuthentication();
app.UseAuthorization();

var v1 = app.MapGroup("/v1").RequireAuthorization();

app.MapJiraWebhooks();
app.MapProjectEndpoints();
app.MapIntegrationEndpoints();
app.MapReportSettingsEndpoints(v1);
app.MapStatsEndpoints(v1);

app.Run();

public sealed class AzureAdB2COptions
{
    [Required] public required string Authority { get; init; }
    [Required] public required string ClientId { get; init; }
}

public partial class Program { }
