using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using Testurio.Api.DTOs;
using Testurio.Api.Services;
using Testurio.Core.Interfaces;
using Testurio.Infrastructure.Cosmos;
using Testurio.Infrastructure.Seeding;

namespace Testurio.IntegrationTests.Controllers;

/// <summary>
/// Integration tests for GET/PUT /v1/admin/prompt-templates/{stage}.
/// Verifies that route validation, auth role enforcement, and service delegation all work
/// end-to-end through the minimal API layer.
/// </summary>
public class AdminPromptTemplateControllerTests : IClassFixture<AdminPromptTemplateControllerTests.ApiFactory>
{
    private readonly ApiFactory _factory;

    public AdminPromptTemplateControllerTests(ApiFactory factory)
    {
        _factory = factory;
        _factory.ResetMocks();
    }

    private HttpClient CreateAdminClient()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "admin-token");
        return client;
    }

    private HttpClient CreateNonAdminClient()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "user-token");
        return client;
    }

    private static PromptTemplateDto MakeDto(string stage = "story_parser") =>
        new(stage, 1, "You are a test assistant.", true, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

    // ─── GET /v1/admin/prompt-templates/{stage} ───────────────────────────────

    [Fact]
    public async Task Get_ValidStage_AdminUser_Returns200()
    {
        var dto = MakeDto("story_parser");
        _factory.AdminServiceMock
            .Setup(s => s.GetAsync("story_parser", It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);

        var client = CreateAdminClient();
        var response = await client.GetAsync("/v1/admin/prompt-templates/story_parser");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<PromptTemplateDto>();
        Assert.NotNull(result);
        Assert.Equal("story_parser", result.Stage);
    }

    [Fact]
    public async Task Get_ValidStage_TemplateNotFound_Returns404()
    {
        _factory.AdminServiceMock
            .Setup(s => s.GetAsync("story_parser", It.IsAny<CancellationToken>()))
            .ReturnsAsync((PromptTemplateDto?)null);

        var client = CreateAdminClient();
        var response = await client.GetAsync("/v1/admin/prompt-templates/story_parser");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Get_InvalidStage_Returns400()
    {
        var client = CreateAdminClient();
        var response = await client.GetAsync("/v1/admin/prompt-templates/invalid_stage");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Get_NonAdminUser_Returns403()
    {
        var client = CreateNonAdminClient();
        var response = await client.GetAsync("/v1/admin/prompt-templates/story_parser");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Get_Unauthenticated_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/v1/admin/prompt-templates/story_parser");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("story_parser")]
    [InlineData("agent_router")]
    [InlineData("api_test_generator")]
    [InlineData("ui_e2e_test_generator")]
    [InlineData("report_writer")]
    public async Task Get_AllValidStages_AreAccepted(string stage)
    {
        _factory.AdminServiceMock
            .Setup(s => s.GetAsync(stage, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeDto(stage));

        var client = CreateAdminClient();
        var response = await client.GetAsync($"/v1/admin/prompt-templates/{stage}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ─── PUT /v1/admin/prompt-templates/{stage} ───────────────────────────────

    [Fact]
    public async Task Put_ValidStageAndBody_AdminUser_Returns200WithUpdatedDto()
    {
        var updated = MakeDto("story_parser") with { Version = 2, Body = "Updated body." };
        _factory.AdminServiceMock
            .Setup(s => s.UpdateAsync("story_parser", "Updated body.", It.IsAny<CancellationToken>()))
            .ReturnsAsync(updated);

        var client = CreateAdminClient();
        var response = await client.PutAsJsonAsync(
            "/v1/admin/prompt-templates/story_parser",
            new UpdatePromptTemplateRequest { Body = "Updated body." });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<PromptTemplateDto>();
        Assert.NotNull(result);
        Assert.Equal(2, result.Version);
        Assert.Equal("Updated body.", result.Body);
    }

    [Fact]
    public async Task Put_InvalidStage_Returns400()
    {
        var client = CreateAdminClient();
        var response = await client.PutAsJsonAsync(
            "/v1/admin/prompt-templates/unknown_stage",
            new UpdatePromptTemplateRequest { Body = "Some body." });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Put_EmptyBody_Returns400()
    {
        var client = CreateAdminClient();
        var response = await client.PutAsJsonAsync(
            "/v1/admin/prompt-templates/story_parser",
            new UpdatePromptTemplateRequest { Body = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Put_TemplateNotFound_Returns404()
    {
        _factory.AdminServiceMock
            .Setup(s => s.UpdateAsync("story_parser", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("PromptTemplate 'story_parser' not found."));

        var client = CreateAdminClient();
        var response = await client.PutAsJsonAsync(
            "/v1/admin/prompt-templates/story_parser",
            new UpdatePromptTemplateRequest { Body = "New body." });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Put_NonAdminUser_Returns403()
    {
        var client = CreateNonAdminClient();
        var response = await client.PutAsJsonAsync(
            "/v1/admin/prompt-templates/story_parser",
            new UpdatePromptTemplateRequest { Body = "Some body." });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ─── Factory ──────────────────────────────────────────────────────────────

    public class ApiFactory : WebApplicationFactory<Program>
    {
        private readonly Mock<IPromptTemplateAdminService> _adminService = new();

        public Mock<IPromptTemplateAdminService> AdminServiceMock => _adminService;

        public void ResetMocks() => _adminService.Reset();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Infrastructure:CosmosConnectionString"] = "AccountEndpoint=https://localhost:8081/;AccountKey=AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA==",
                    ["Infrastructure:CosmosDatabaseName"] = "TestDb",
                    ["Infrastructure:ServiceBusConnectionString"] = "Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=dummykey==",
                    ["Infrastructure:TestRunJobQueueName"] = "test-runs",
                    ["Infrastructure:BlobStorageConnectionString"] = "UseDevelopmentStorage=true",
                    ["Infrastructure:ExecutionLogsBlobContainerName"] = "execution-logs",
                    ["Infrastructure:ReportTemplatesBlobContainerName"] = "report-templates",
                    ["Infrastructure:ReportsBlobContainerName"] = "reports",
                    ["AzureAdB2C:Authority"] = "https://login.microsoftonline.com/test-tenant",
                    ["AzureAdB2C:ClientId"] = "test-client-id"
                });
            });

            builder.ConfigureTestServices(services =>
            {
                services.Replace(ServiceDescriptor.Scoped<IPromptTemplateAdminService>(_ => _adminService.Object));
                services.Replace(ServiceDescriptor.Singleton<ICosmosDbInitializer>(_ => new NoOpCosmosDbInitializer()));
                services.Replace(ServiceDescriptor.Singleton<IPromptTemplateSeeder>(_ => new NoOpPromptTemplateSeeder()));
                services.Replace(ServiceDescriptor.Singleton<IPlanSeeder>(_ => new NoOpPlanSeeder()));

                // Two-scheme auth: "Admin" issues principal with the admin role claim,
                // "Test" issues a plain user principal without the role.
                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = "Multi";
                    options.DefaultChallengeScheme = "Multi";
                })
                .AddPolicyScheme("Multi", "Multi", opts =>
                {
                    opts.ForwardDefaultSelector = ctx =>
                    {
                        if (ctx.Request.Headers.Authorization.FirstOrDefault()?.Contains("admin") == true)
                            return "Admin";
                        if (ctx.Request.Headers.Authorization.Count > 0)
                            return "User";
                        return "User";
                    };
                })
                .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions,
                    AdminTestAuthHandler>("Admin", _ => { })
                .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions,
                    PlainTestAuthHandler>("User", _ => { });
            });
        }
    }
}

/// <summary>
/// Issues a ClaimsPrincipal with the "admin" role claim — passes the "admin" policy.
/// </summary>
internal sealed class AdminTestAuthHandler(
    Microsoft.Extensions.Options.IOptionsMonitor<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions> options,
    Microsoft.Extensions.Logging.ILoggerFactory logger,
    System.Text.Encodings.Web.UrlEncoder encoder)
    : Microsoft.AspNetCore.Authentication.AuthenticationHandler<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<Microsoft.AspNetCore.Authentication.AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.ContainsKey("Authorization"))
            return Task.FromResult(Microsoft.AspNetCore.Authentication.AuthenticateResult.Fail("No Authorization header"));

        var claims = new[]
        {
            new System.Security.Claims.Claim("oid", "admin-user-oid"),
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, "admin"),
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Name, "Admin User"),
        };
        var identity = new System.Security.Claims.ClaimsIdentity(claims, "Admin");
        var principal = new System.Security.Claims.ClaimsPrincipal(identity);
        var ticket = new Microsoft.AspNetCore.Authentication.AuthenticationTicket(principal, "Admin");
        return Task.FromResult(Microsoft.AspNetCore.Authentication.AuthenticateResult.Success(ticket));
    }
}

/// <summary>
/// Issues a ClaimsPrincipal without the "admin" role claim — fails the "admin" policy.
/// </summary>
internal sealed class PlainTestAuthHandler(
    Microsoft.Extensions.Options.IOptionsMonitor<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions> options,
    Microsoft.Extensions.Logging.ILoggerFactory logger,
    System.Text.Encodings.Web.UrlEncoder encoder)
    : Microsoft.AspNetCore.Authentication.AuthenticationHandler<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<Microsoft.AspNetCore.Authentication.AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.ContainsKey("Authorization"))
            return Task.FromResult(Microsoft.AspNetCore.Authentication.AuthenticateResult.Fail("No Authorization header"));

        var claims = new[]
        {
            new System.Security.Claims.Claim("oid", "plain-user-oid"),
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Name, "Plain User"),
        };
        var identity = new System.Security.Claims.ClaimsIdentity(claims, "User");
        var principal = new System.Security.Claims.ClaimsPrincipal(identity);
        var ticket = new Microsoft.AspNetCore.Authentication.AuthenticationTicket(principal, "User");
        return Task.FromResult(Microsoft.AspNetCore.Authentication.AuthenticateResult.Success(ticket));
    }
}
