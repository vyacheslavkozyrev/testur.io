using Testurio.Infrastructure.Seeding;
using Testurio.Infrastructure.Cosmos;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using Testurio.Api.DTOs;
using Testurio.Core.Entities;
using Testurio.Core.Interfaces;
using Testurio.Core.Repositories;
using Testurio.Infrastructure;

namespace Testurio.IntegrationTests.Controllers;

/// <summary>
/// Integration tests for /v1/account endpoints.
/// Uses a fake JWT so that RequireAuthorization() passes without a real B2C token.
/// </summary>
public class AccountControllerTests : IClassFixture<AccountControllerTests.ApiFactory>
{
    private readonly ApiFactory _factory;

    public AccountControllerTests(ApiFactory factory)
    {
        _factory = factory;
        _factory.ResetMocks();
    }

    private static UserDocument MakeUserDocument(string userId = "test-user-oid") =>
        new() { Id = userId, UserId = userId, FirstName = "Test", LastName = "User", Language = "en", Theme = "light" };

    private HttpClient CreateAuthenticatedClient()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "test-token");
        return client;
    }

    // ─── GET /v1/account/profile ─────────────────────────────────────────────

    [Fact]
    public async Task GetProfile_Returns200_WithName_WhenUserExists()
    {
        var doc = MakeUserDocument();
        _factory.UserRepoMock
            .Setup(r => r.GetByUserIdAsync("test-user-oid", It.IsAny<CancellationToken>()))
            .ReturnsAsync(doc);

        var client = CreateAuthenticatedClient();
        var response = await client.GetAsync("/v1/account/profile");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AccountProfileDto>();
        Assert.NotNull(body);
        Assert.Equal("test-user-oid", body.UserId);
        Assert.Equal("Test", body.FirstName);
        Assert.Equal("User", body.LastName);
    }

    [Fact]
    public async Task GetProfile_Returns200_WithNullName_WhenUserNotFound()
    {
        _factory.UserRepoMock
            .Setup(r => r.GetByUserIdAsync("test-user-oid", It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserDocument?)null);

        var client = CreateAuthenticatedClient();
        var response = await client.GetAsync("/v1/account/profile");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AccountProfileDto>();
        Assert.NotNull(body);
        Assert.Equal("test-user-oid", body.UserId);
        Assert.Null(body.FirstName);
        Assert.Null(body.LastName);
    }

    // ─── PATCH /v1/account/profile ────────────────────────────────────────────

    [Fact]
    public async Task PatchProfile_Returns200_WithUpdatedName()
    {
        _factory.UserRepoMock
            .Setup(r => r.GetByUserIdAsync("test-user-oid", It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserDocument?)null);
        _factory.UserRepoMock
            .Setup(r => r.UpsertAsync(It.IsAny<UserDocument>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserDocument doc, CancellationToken _) => doc);

        var client = CreateAuthenticatedClient();
        var payload = new { firstName = "New", lastName = "Name" };
        var response = await client.PatchAsJsonAsync("/v1/account/profile", payload);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AccountProfileDto>();
        Assert.NotNull(body);
        Assert.Equal("New", body.FirstName);
        Assert.Equal("Name", body.LastName);
    }

    [Fact]
    public async Task PatchProfile_Returns400_WhenFirstNameExceeds100Chars()
    {
        var client = CreateAuthenticatedClient();
        var payload = new { firstName = new string('a', 101) };
        var response = await client.PatchAsJsonAsync("/v1/account/profile", payload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var body = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(jsonOptions);
        Assert.NotNull(body);
        Assert.Contains("FirstName", body.Errors.Keys, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PatchProfile_Returns400_WhenLastNameExceeds100Chars()
    {
        var client = CreateAuthenticatedClient();
        var payload = new { lastName = new string('a', 101) };
        var response = await client.PatchAsJsonAsync("/v1/account/profile", payload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var body = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(jsonOptions);
        Assert.NotNull(body);
        Assert.Contains("LastName", body.Errors.Keys, StringComparer.OrdinalIgnoreCase);
    }

    // ─── GET /v1/account/preferences ─────────────────────────────────────────

    [Fact]
    public async Task GetPreferences_Returns200_WhenUserExists()
    {
        var doc = MakeUserDocument();
        _factory.UserRepoMock
            .Setup(r => r.GetByUserIdAsync("test-user-oid", It.IsAny<CancellationToken>()))
            .ReturnsAsync(doc);

        var client = CreateAuthenticatedClient();
        var response = await client.GetAsync("/v1/account/preferences");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AccountPreferencesDto>();
        Assert.NotNull(body);
        Assert.Equal("en", body.Language);
        Assert.Equal("light", body.Theme);
    }

    [Fact]
    public async Task GetPreferences_Returns404_WhenUserNotFound()
    {
        _factory.UserRepoMock
            .Setup(r => r.GetByUserIdAsync("test-user-oid", It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserDocument?)null);

        var client = CreateAuthenticatedClient();
        var response = await client.GetAsync("/v1/account/preferences");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ─── PATCH /v1/account/preferences ───────────────────────────────────────

    [Fact]
    public async Task PatchPreferences_Returns200_WithMergedPreferences()
    {
        var doc = MakeUserDocument();
        _factory.UserRepoMock
            .Setup(r => r.GetByUserIdAsync("test-user-oid", It.IsAny<CancellationToken>()))
            .ReturnsAsync(doc);
        _factory.UserRepoMock
            .Setup(r => r.UpsertAsync(It.IsAny<UserDocument>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserDocument d, CancellationToken _) => d);

        var client = CreateAuthenticatedClient();
        var payload = new { theme = "dark" };
        var response = await client.PatchAsJsonAsync("/v1/account/preferences", payload);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AccountPreferencesDto>();
        Assert.NotNull(body);
        Assert.Equal("dark", body.Theme);
        // Language should be unchanged
        Assert.Equal("en", body.Language);
    }

    [Fact]
    public async Task PatchPreferences_Returns400_WhenLanguageIsInvalid()
    {
        var client = CreateAuthenticatedClient();
        var payload = new { language = "fr" };
        var response = await client.PatchAsJsonAsync("/v1/account/preferences", payload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var body = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(jsonOptions);
        Assert.NotNull(body);
        Assert.Contains("Language", body.Errors.Keys, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PatchPreferences_Returns400_WhenThemeIsInvalid()
    {
        var client = CreateAuthenticatedClient();
        var payload = new { theme = "sepia" };
        var response = await client.PatchAsJsonAsync("/v1/account/preferences", payload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var body = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(jsonOptions);
        Assert.NotNull(body);
        Assert.Contains("Theme", body.Errors.Keys, StringComparer.OrdinalIgnoreCase);
    }

    // ─── Auth guard ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GetProfile_Returns401_WithoutAuthToken()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/v1/account/profile");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    public class ApiFactory : WebApplicationFactory<Program>
    {
        private readonly Mock<IUserRepository> _userRepo = new();
        private readonly Mock<IProjectRepository> _projectRepo = new();

        public Mock<IUserRepository> UserRepoMock => _userRepo;

        public void ResetMocks()
        {
            _userRepo.Reset();
            _projectRepo.Reset();
        }

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
                    ["AzureAdB2C:ClientId"] = "test-client-id",
                    ["App:BaseUrl"] = "https://localhost"
                });
            });

            builder.ConfigureTestServices(services =>
            {
                services.Replace(ServiceDescriptor.Singleton<IUserRepository>(_ => _userRepo.Object));
                services.Replace(ServiceDescriptor.Singleton<IProjectRepository>(_ => _projectRepo.Object));
                services.Replace(ServiceDescriptor.Singleton<ISecretResolver>(_ => new PassthroughSecretResolver())); services.Replace(ServiceDescriptor.Singleton<ICosmosDbInitializer>(_ => new NoOpCosmosDbInitializer()));
                services.Replace(ServiceDescriptor.Singleton<IPromptTemplateSeeder>(_ => new NoOpPromptTemplateSeeder()));
                services.Replace(ServiceDescriptor.Singleton<IPlanSeeder>(_ => new NoOpPlanSeeder()));

                services.AddAuthentication("Test")
                    .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions,
                        AccountTestAuthHandler>("Test", _ => { });
            });
        }
    }
}

internal sealed class AccountTestAuthHandler(
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
            new System.Security.Claims.Claim("oid", "test-user-oid"),
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Name, "Test User"),
        };
        var identity = new System.Security.Claims.ClaimsIdentity(claims, "Test");
        var principal = new System.Security.Claims.ClaimsPrincipal(identity);
        var ticket = new Microsoft.AspNetCore.Authentication.AuthenticationTicket(principal, "Test");
        return Task.FromResult(Microsoft.AspNetCore.Authentication.AuthenticateResult.Success(ticket));
    }
}



