using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Testurio.Api.Controllers;
using Testurio.Api.Endpoints;
using Testurio.Api.Middleware;
using Testurio.Api.Options;
using Testurio.Api.Services;
using Testurio.Api.Webhooks;
using Testurio.Core.Interfaces;
using Testurio.Infrastructure;
using Testurio.Infrastructure.Anthropic;
using Testurio.Infrastructure.Blob;
using Testurio.Infrastructure.Cosmos;
using Testurio.Infrastructure.KeyVault;
using Testurio.Infrastructure.Options;
using Testurio.Infrastructure.Seeding;
using Testurio.Infrastructure.Security;

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
builder.Services.AddAuthorization(opts =>
{
    opts.AddPolicy("admin", policy => policy.RequireRole("admin"));
});
builder.Services.AddCors(options =>
{
    options.AddPolicy("DevPortal", policy =>
        policy.WithOrigins("http://localhost:3000")
              .AllowAnyHeader()
              .AllowAnyMethod());
});
// ── Secrets (Key Vault in production; local config in development) ────────────
// Must be called before AddInfrastructure() because factories depend on the singletons.
builder.Services.AddKeyVaultSecretLoader(builder.Configuration, builder.Environment);
await builder.Services.AddInfrastructureSecretsAsync(builder.Configuration, builder.Environment);
await builder.Services.AddAnthropicSecretsAsync(builder.Configuration, builder.Environment);
await builder.Services.AddStripeSecretsAsync(builder.Configuration, builder.Environment);

builder.Services.AddInfrastructure();
builder.Services.AddStripe();

// ILlmGenerationClient — used by PromptCheckService for AI-assisted prompt quality checks.
// The API key is sourced from AnthropicSecrets (populated above); if absent (empty string) the
// prompt-check endpoint will fail gracefully at runtime.
builder.Services.AddHttpClient<ILlmGenerationClient, AnthropicGenerationClient>((sp, client) =>
{
    var secrets = sp.GetRequiredService<AnthropicSecrets>();
    if (!string.IsNullOrEmpty(secrets.ApiKey))
        client.DefaultRequestHeaders.Add("x-api-key", secrets.ApiKey);
})
.AddTypedClient<ILlmGenerationClient>((client, sp) =>
{
    var modelId = builder.Configuration["Claude:ModelId"] ?? "claude-opus-4-7";
    var logger = sp.GetRequiredService<ILogger<AnthropicGenerationClient>>();
    return new AnthropicGenerationClient(client, modelId, logger);
});

builder.Services.AddScoped<IWorkItemTypeFilterService, WorkItemTypeFilterService>();
builder.Services.AddScoped<IJiraWebhookService, JiraWebhookService>();
builder.Services.AddScoped<IADOWebhookService, ADOWebhookService>();
builder.Services.AddScoped<IProjectService, ProjectService>();
builder.Services.AddScoped<IPMToolConnectionService, PMToolConnectionService>();
builder.Services.AddScoped<IPromptCheckService, PromptCheckService>();
builder.Services.AddScoped<IReportTemplateService, ReportTemplateService>();
builder.Services.AddScoped<IProjectAccessService, ProjectAccessService>();
builder.Services.AddScoped<IProjectApiAuthService, ProjectApiAuthService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<IProjectHistoryService, ProjectHistoryService>();
builder.Services.AddScoped<IAccountService, AccountService>();
builder.Services.AddScoped<IBillingService, BillingService>();

// Feature 0047: admin service for prompt template management.
builder.Services.AddScoped<IPromptTemplateAdminService, PromptTemplateAdminService>();

builder.Services.AddOptions<AppOptions>()
    .BindConfiguration("App")
    .ValidateDataAnnotations()
    .ValidateOnStart();

// Feature 0043: SSE relay — subscribe to run-status-changed Service Bus messages and fan out to SSE channels.
builder.Services.AddSingleton<DashboardEventRelay>(sp =>
{
    var sbClient = sp.GetRequiredService<Azure.Messaging.ServiceBus.ServiceBusClient>();
    var topicName = builder.Configuration["Infrastructure:RunStatusChangedQueueName"] ?? "run-status-changed";
    var streamManager = sp.GetRequiredService<IDashboardStreamManager>();
    var logger = sp.GetRequiredService<ILogger<DashboardEventRelay>>();
    return new DashboardEventRelay(sbClient, topicName, streamManager, logger);
});
builder.Services.AddHostedService(sp => sp.GetRequiredService<DashboardEventRelay>());

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

// ISecretResolver handles project-level credential secrets (Basic Auth, header tokens).
// In production it delegates to the already-registered IKeyVaultSecretLoader so we reuse
// the same SecretClient and retry logic rather than constructing a second one independently.
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

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var startupLogger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    try
    {
        var initializer = scope.ServiceProvider.GetRequiredService<ICosmosDbInitializer>();
        await initializer.InitializeAsync();
    }
    catch (Exception ex)
    {
        startupLogger.LogCritical(ex, "Cosmos DB initialization failed. API cannot start.");
        throw;
    }

    try
    {
        var promptSeeder = scope.ServiceProvider.GetRequiredService<IPromptTemplateSeeder>();
        await promptSeeder.SeedAsync();
    }
    catch (Exception ex)
    {
        startupLogger.LogCritical(ex, "Prompt template seeding failed. API cannot start.");
        throw;
    }

    try
    {
        var planSeeder = scope.ServiceProvider.GetRequiredService<IPlanSeeder>();
        await planSeeder.SeedAsync();
    }
    catch (Exception ex)
    {
        startupLogger.LogCritical(ex, "Plan seeding failed. API cannot start.");
        throw;
    }
}

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

// Feature 0047: admin prompt template management endpoints.
// Require the "admin" role policy so only authorised operators can access them.
var adminGroup = v1.MapGroup("/admin").RequireAuthorization("admin");
app.MapAdminPromptTemplateEndpoints(adminGroup);

v1.MapPlanEndpoints();
v1.MapAccountEndpoints();
v1.MapBillingEndpoints();
app.MapStripeWebhook();
app.MapJiraWebhooks();
app.MapAdoCommentsWebhook();
app.MapJiraCommentsWebhook();
app.MapProjectEndpoints();
app.MapProjectAccessEndpoints(v1);
app.MapProjectApiAuthEndpoints(v1);
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
