using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
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
/// Integration tests for /v1/projects/{projectId}/access endpoints.
/// </summary>
public class ProjectAccessControllerTests : IClassFixture<ProjectAccessControllerTests.ApiFactory>
{
    private readonly ApiFactory _factory;

    public ProjectAccessControllerTests(ApiFactory factory)
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
        AccessMode = AccessMode.IpAllowlist,
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow,
    };

    private HttpClient CreateAuthenticatedClient()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "test-token");
        return client;
    }

    // ─── GET /v1/projects/{projectId}/access ─────────────────────────────────

    [Fact]
    public async Task GetProjectAccess_Returns200_WithIpAllowlistMode()
    {
        var project = MakeProject();
        _factory.ProjectRepoMock
            .Setup(r => r.GetByProjectIdAsync("proj-001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(project);
        _factory.ProjectRepoMock
            .Setup(r => r.GetByIdAsync(It.IsAny<string>(), "proj-001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(project);

        var client = CreateAuthenticatedClient();
        var response = await client.GetAsync("/v1/projects/proj-001/access");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ProjectAccessDto>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(body);
        Assert.Equal(AccessMode.IpAllowlist, body.AccessMode);
        Assert.Null(body.BasicAuthUser);
        Assert.Null(body.HeaderTokenName);
    }

    [Fact]
    public async Task GetProjectAccess_Returns404_WhenProjectNotFound()
    {
        _factory.ProjectRepoMock
            .Setup(r => r.GetByProjectIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Project?)null);

        var client = CreateAuthenticatedClient();
        var response = await client.GetAsync("/v1/projects/does-not-exist/access");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetProjectAccess_Returns403_WhenProjectBelongsToDifferentUser()
    {
        var otherProject = MakeProject(userId: "other-user-oid");
        _factory.ProjectRepoMock
            .Setup(r => r.GetByProjectIdAsync("proj-001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(otherProject);

        var client = CreateAuthenticatedClient();
        var response = await client.GetAsync("/v1/projects/proj-001/access");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetProjectAccess_Returns401_WithoutAuthToken()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/v1/projects/proj-001/access");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ─── PATCH /v1/projects/{projectId}/access — IpAllowlist ─────────────────

    [Fact]
    public async Task PatchProjectAccess_Returns200_WithIpAllowlistMode()
    {
        var project = MakeProject();
        _factory.ProjectRepoMock
            .Setup(r => r.GetByProjectIdAsync("proj-001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(project);
        _factory.ProjectRepoMock
            .Setup(r => r.GetByIdAsync(It.IsAny<string>(), "proj-001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(project);
        _factory.ProjectRepoMock
            .Setup(r => r.UpdateAsync(It.IsAny<Project>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Project p, CancellationToken _) => p);

        var client = CreateAuthenticatedClient();
        var payload = new { accessMode = "ipAllowlist" };
        var response = await client.PatchAsJsonAsync("/v1/projects/proj-001/access", payload);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ProjectAccessDto>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(body);
        Assert.Equal(AccessMode.IpAllowlist, body.AccessMode);
    }

    // ─── PATCH — BasicAuth ────────────────────────────────────────────────────

    [Fact]
    public async Task PatchProjectAccess_Returns200_WithBasicAuthMode()
    {
        var project = MakeProject();
        _factory.ProjectRepoMock
            .Setup(r => r.GetByProjectIdAsync("proj-001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(project);
        _factory.ProjectRepoMock
            .Setup(r => r.GetByIdAsync(It.IsAny<string>(), "proj-001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(project);
        _factory.ProjectRepoMock
            .Setup(r => r.UpdateAsync(It.IsAny<Project>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Project p, CancellationToken _) => p);

        var client = CreateAuthenticatedClient();
        var payload = new { accessMode = "basicAuth", basicAuthUser = "admin", basicAuthPass = "s3cret" };
        var response = await client.PatchAsJsonAsync("/v1/projects/proj-001/access", payload);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ProjectAccessDto>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(body);
        Assert.Equal(AccessMode.BasicAuth, body.AccessMode);
        // Response must not expose the password — basic_auth_user can be pre-filled
        Assert.Null(body.HeaderTokenName);
    }

    [Fact]
    public async Task PatchProjectAccess_Returns400_WhenBasicAuthUserMissing()
    {
        var client = CreateAuthenticatedClient();
        var payload = new { accessMode = "basicAuth", basicAuthPass = "s3cret" };
        var response = await client.PatchAsJsonAsync("/v1/projects/proj-001/access", payload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PatchProjectAccess_Returns400_WhenBasicAuthPassMissing()
    {
        var client = CreateAuthenticatedClient();
        var payload = new { accessMode = "basicAuth", basicAuthUser = "admin" };
        var response = await client.PatchAsJsonAsync("/v1/projects/proj-001/access", payload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ─── PATCH — HeaderToken ──────────────────────────────────────────────────

    [Fact]
    public async Task PatchProjectAccess_Returns200_WithHeaderTokenMode()
    {
        var project = MakeProject();
        _factory.ProjectRepoMock
            .Setup(r => r.GetByProjectIdAsync("proj-001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(project);
        _factory.ProjectRepoMock
            .Setup(r => r.GetByIdAsync(It.IsAny<string>(), "proj-001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(project);
        _factory.ProjectRepoMock
            .Setup(r => r.UpdateAsync(It.IsAny<Project>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Project p, CancellationToken _) => p);

        var client = CreateAuthenticatedClient();
        var payload = new { accessMode = "headerToken", headerTokenName = "X-Testurio-Token", headerTokenValue = "tok-abc" };
        var response = await client.PatchAsJsonAsync("/v1/projects/proj-001/access", payload);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ProjectAccessDto>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(body);
        Assert.Equal(AccessMode.HeaderToken, body.AccessMode);
        Assert.Equal("X-Testurio-Token", body.HeaderTokenName);
        Assert.Null(body.BasicAuthUser);
    }

    [Fact]
    public async Task PatchProjectAccess_Returns400_WhenHeaderTokenNameMissing()
    {
        var client = CreateAuthenticatedClient();
        var payload = new { accessMode = "headerToken", headerTokenValue = "tok-abc" };
        var response = await client.PatchAsJsonAsync("/v1/projects/proj-001/access", payload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PatchProjectAccess_Returns400_WhenHeaderTokenValueMissing()
    {
        var client = CreateAuthenticatedClient();
        var payload = new { accessMode = "headerToken", headerTokenName = "X-Testurio-Token" };
        var response = await client.PatchAsJsonAsync("/v1/projects/proj-001/access", payload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PatchProjectAccess_Returns400_WhenHeaderTokenNameContainsSpaces()
    {
        var client = CreateAuthenticatedClient();
        var payload = new { accessMode = "headerToken", headerTokenName = "X Testurio Token", headerTokenValue = "tok-abc" };
        var response = await client.PatchAsJsonAsync("/v1/projects/proj-001/access", payload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PatchProjectAccess_Returns400_WhenAccessModeIsUnrecognised()
    {
        var client = CreateAuthenticatedClient();
        var payload = new { accessMode = "unknown_mode" };
        var response = await client.PatchAsJsonAsync("/v1/projects/proj-001/access", payload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PatchProjectAccess_Returns403_WhenProjectBelongsToDifferentUser()
    {
        var otherProject = MakeProject(userId: "other-user-oid");
        _factory.ProjectRepoMock
            .Setup(r => r.GetByProjectIdAsync("proj-001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(otherProject);

        var client = CreateAuthenticatedClient();
        var payload = new { accessMode = "ipAllowlist" };
        var response = await client.PatchAsJsonAsync("/v1/projects/proj-001/access", payload);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PatchProjectAccess_Returns404_WhenProjectNotFound()
    {
        _factory.ProjectRepoMock
            .Setup(r => r.GetByProjectIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Project?)null);

        var client = CreateAuthenticatedClient();
        var payload = new { accessMode = "ipAllowlist" };
        var response = await client.PatchAsJsonAsync("/v1/projects/does-not-exist/access", payload);

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
                    ["Infrastructure:CosmosConnectionString"] = "AccountEndpoint=https://localhost:8081/;AccountKey=dummykey==",
                    ["Infrastructure:CosmosDatabaseName"] = "TestDb",
                    ["Infrastructure:ServiceBusConnectionString"] = "Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=dummykey==",
                    ["Infrastructure:TestRunJobQueueName"] = "test-runs",
                    ["Infrastructure:BlobStorageConnectionString"] = "UseDevelopmentStorage=true",
                    ["Infrastructure:ExecutionLogsBlobContainerName"] = "execution-logs",
                    ["Infrastructure:ReportTemplatesBlobContainerName"] = "report-templates",
                    ["Infrastructure:ReportsBlobContainerName"] = "reports",
                    ["AzureAdB2C:Authority"] = "https://login.microsoftonline.com/test-tenant",
                    ["AzureAdB2C:ClientId"] = "test-client-id",
                });
            });

            builder.ConfigureTestServices(services =>
            {
                services.Replace(ServiceDescriptor.Singleton<IProjectRepository>(_ => _projectRepo.Object));
                services.Replace(ServiceDescriptor.Singleton<ITestRunRepository>(_ => _testRunRepo.Object));
                services.Replace(ServiceDescriptor.Singleton<IRunQueueRepository>(_ => _runQueueRepo.Object));
                services.Replace(ServiceDescriptor.Singleton<ITestRunJobSender>(_ => _jobSender.Object));
                services.Replace(ServiceDescriptor.Singleton<IJiraApiClient>(_ => _jiraApiClient.Object));
                services.Replace(ServiceDescriptor.Singleton<ISecretResolver>(_ => new PassthroughSecretResolver()));

                services.AddAuthentication("Test")
                    .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions,
                        TestAuthHandler>("Test", _ => { });
            });
        }
    }
}
