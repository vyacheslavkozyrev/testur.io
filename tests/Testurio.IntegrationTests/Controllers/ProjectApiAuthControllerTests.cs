using Testurio.Infrastructure.Seeding;
using Testurio.Infrastructure.Cosmos;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using Testurio.Api.DTOs;
using Testurio.Core.Entities;
using Testurio.Core.Enums;
using Testurio.Core.Interfaces;
using Testurio.Core.Repositories;
using Testurio.Infrastructure;

namespace Testurio.IntegrationTests.Controllers;

/// <summary>
/// Integration tests for /v1/projects/{projectId}/api-auth endpoints.
/// </summary>
public class ProjectApiAuthControllerTests : IClassFixture<ProjectApiAuthControllerTests.ApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly ApiFactory _factory;

    public ProjectApiAuthControllerTests(ApiFactory factory)
    {
        _factory = factory;
        _factory.ResetMocks();
    }

    private static Project MakeProject(string userId = "test-user-oid") => new()
    {
        Id = "proj-001",
        UserId = userId,
        Name = "Test Project",
        ProductUrl = "https://app.example.com",
        TestingStrategy = "API smoke tests.",
        ApiAuthMethod = ApiAuthMethod.None,
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow,
    };

    private HttpClient CreateAuthenticatedClient()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "test-token");
        return client;
    }

    // ─── PATCH — valid Bearer ─────────────────────────────────────────────────

    [Fact]
    public async Task PatchProjectApiAuth_Returns200_WithBearerTokenConfiguredTrue()
    {
        var project = MakeProject();
        _factory.ProjectRepoMock
            .Setup(r => r.GetByProjectIdAsync("proj-001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(project);
        _factory.ProjectRepoMock
            .Setup(r => r.UpdateAsync(It.IsAny<Project>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Project p, CancellationToken _) => p);

        var client = CreateAuthenticatedClient();
        var payload = new { apiAuthMethod = "bearer", apiAuthBearerToken = "tok-secret" };
        var response = await client.PatchAsJsonAsync("/v1/projects/proj-001/api-auth", payload);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ProjectApiAuthDto>(JsonOptions);
        Assert.NotNull(body);
        Assert.Equal("bearer", body.ApiAuthMethod);
        Assert.True(body.ApiAuthBearerTokenConfigured);
    }

    // ─── PATCH — bearer without token ────────────────────────────────────────

    [Fact]
    public async Task PatchProjectApiAuth_Returns400_WhenBearerTokenMissing()
    {
        var client = CreateAuthenticatedClient();
        var payload = new { apiAuthMethod = "bearer" };
        var response = await client.PatchAsJsonAsync("/v1/projects/proj-001/api-auth", payload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ─── PATCH — api_key without name ────────────────────────────────────────

    [Fact]
    public async Task PatchProjectApiAuth_Returns400_WhenApiKeyNameMissing()
    {
        var client = CreateAuthenticatedClient();
        var payload = new { apiAuthMethod = "api_key", apiAuthApiKeyPlacement = "header", apiAuthApiKeyValue = "val" };
        var response = await client.PatchAsJsonAsync("/v1/projects/proj-001/api-auth", payload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ─── PATCH — api_key without placement ───────────────────────────────────

    [Fact]
    public async Task PatchProjectApiAuth_Returns400_WhenApiKeyPlacementMissing()
    {
        var client = CreateAuthenticatedClient();
        var payload = new { apiAuthMethod = "api_key", apiAuthApiKeyName = "X-Api-Key", apiAuthApiKeyValue = "val" };
        var response = await client.PatchAsJsonAsync("/v1/projects/proj-001/api-auth", payload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ─── PATCH — basic without username ──────────────────────────────────────

    [Fact]
    public async Task PatchProjectApiAuth_Returns400_WhenBasicUsernameMissing()
    {
        var client = CreateAuthenticatedClient();
        var payload = new { apiAuthMethod = "basic", apiAuthBasicPassword = "pass" };
        var response = await client.PatchAsJsonAsync("/v1/projects/proj-001/api-auth", payload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ─── PATCH — none ─────────────────────────────────────────────────────────

    [Fact]
    public async Task PatchProjectApiAuth_Returns200_WithNoneMethod()
    {
        var project = MakeProject();
        _factory.ProjectRepoMock
            .Setup(r => r.GetByProjectIdAsync("proj-001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(project);
        _factory.ProjectRepoMock
            .Setup(r => r.UpdateAsync(It.IsAny<Project>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Project p, CancellationToken _) => p);

        var client = CreateAuthenticatedClient();
        var payload = new { apiAuthMethod = "none" };
        var response = await client.PatchAsJsonAsync("/v1/projects/proj-001/api-auth", payload);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ProjectApiAuthDto>(JsonOptions);
        Assert.NotNull(body);
        Assert.Equal("none", body.ApiAuthMethod);
    }

    // ─── PATCH — wrong user → 403 ─────────────────────────────────────────────

    [Fact]
    public async Task PatchProjectApiAuth_Returns403_WhenProjectBelongsToDifferentUser()
    {
        var otherProject = MakeProject(userId: "other-user-oid");
        _factory.ProjectRepoMock
            .Setup(r => r.GetByProjectIdAsync("proj-001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(otherProject);

        var client = CreateAuthenticatedClient();
        var payload = new { apiAuthMethod = "none" };
        var response = await client.PatchAsJsonAsync("/v1/projects/proj-001/api-auth", payload);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ─── PATCH — unknown project → 404 ───────────────────────────────────────

    [Fact]
    public async Task PatchProjectApiAuth_Returns404_WhenProjectNotFound()
    {
        _factory.ProjectRepoMock
            .Setup(r => r.GetByProjectIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Project?)null);

        var client = CreateAuthenticatedClient();
        var payload = new { apiAuthMethod = "none" };
        var response = await client.PatchAsJsonAsync("/v1/projects/does-not-exist/api-auth", payload);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ─── GET ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetProjectApiAuth_Returns200_WithNoneForNewProject()
    {
        var project = MakeProject();
        _factory.ProjectRepoMock
            .Setup(r => r.GetByProjectIdAsync("proj-001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(project);

        var client = CreateAuthenticatedClient();
        var response = await client.GetAsync("/v1/projects/proj-001/api-auth");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ProjectApiAuthDto>(JsonOptions);
        Assert.NotNull(body);
        Assert.Equal("none", body.ApiAuthMethod);
        Assert.Null(body.ApiAuthBearerTokenConfigured);
    }

    [Fact]
    public async Task GetProjectApiAuth_Returns403_WhenProjectBelongsToDifferentUser()
    {
        var otherProject = MakeProject(userId: "other-user-oid");
        _factory.ProjectRepoMock
            .Setup(r => r.GetByProjectIdAsync("proj-001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(otherProject);

        var client = CreateAuthenticatedClient();
        var response = await client.GetAsync("/v1/projects/proj-001/api-auth");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetProjectApiAuth_Returns404_WhenProjectNotFound()
    {
        _factory.ProjectRepoMock
            .Setup(r => r.GetByProjectIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Project?)null);

        var client = CreateAuthenticatedClient();
        var response = await client.GetAsync("/v1/projects/does-not-exist/api-auth");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    public class ApiFactory : WebApplicationFactory<Program>
    {
        private readonly Mock<IProjectRepository> _projectRepo = new();
        private readonly Mock<ITestRunRepository> _testRunRepo = new();
        private readonly Mock<IRunQueueRepository> _runQueueRepo = new();
        private readonly Mock<ITestRunJobSender> _jobSender = new();
        private readonly Mock<IJiraApiClient> _jiraApiClient = new();

        public Mock<IProjectRepository> ProjectRepoMock => _projectRepo;

        public void ResetMocks()
        {
            _projectRepo.Reset();
            _testRunRepo.Reset();
            _runQueueRepo.Reset();
            _jobSender.Reset();
            _jiraApiClient.Reset();
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
                    ["App:BaseUrl"] = "https://localhost",
                });
            });

            builder.ConfigureTestServices(services =>
            {
                services.Replace(ServiceDescriptor.Singleton<IProjectRepository>(_ => _projectRepo.Object));
                services.Replace(ServiceDescriptor.Singleton<ITestRunRepository>(_ => _testRunRepo.Object));
                services.Replace(ServiceDescriptor.Singleton<IRunQueueRepository>(_ => _runQueueRepo.Object));
                services.Replace(ServiceDescriptor.Singleton<ITestRunJobSender>(_ => _jobSender.Object));
                services.Replace(ServiceDescriptor.Singleton<IJiraApiClient>(_ => _jiraApiClient.Object));
                services.Replace(ServiceDescriptor.Singleton<ISecretResolver>(_ => new PassthroughSecretResolver())); services.Replace(ServiceDescriptor.Singleton<ICosmosDbInitializer>(_ => new NoOpCosmosDbInitializer()));
                services.Replace(ServiceDescriptor.Singleton<IPromptTemplateSeeder>(_ => new NoOpPromptTemplateSeeder()));
                services.Replace(ServiceDescriptor.Singleton<IPlanSeeder>(_ => new NoOpPlanSeeder()));

                services.AddAuthentication("Test")
                    .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions,
                        TestAuthHandler>("Test", _ => { });
            });
        }
    }
}



