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
/// Integration tests for /v1/projects endpoints.
/// Uses a fake JWT so that RequireAuthorization() passes without a real B2C token.
/// </summary>
public class ProjectControllerTests : IClassFixture<ProjectControllerTests.ApiFactory>
{
    private readonly ApiFactory _factory;

    public ProjectControllerTests(ApiFactory factory)
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
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow,
    };

    private HttpClient CreateAuthenticatedClient()
    {
        // The test host uses a no-op auth handler that accepts any bearer token.
        var client = _factory.CreateClient();
        // Pass a dummy Bearer token — the test host replaces authentication with a passthrough.
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "test-token");
        return client;
    }

    // ─── GET /v1/projects ────────────────────────────────────────────────────

    [Fact]
    public async Task GetProjects_ReturnsEmptyArray_WhenNoProjects()
    {
        _factory.ProjectRepoMock
            .Setup(r => r.ListByUserAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Project>());

        var client = CreateAuthenticatedClient();
        var response = await client.GetAsync("/v1/projects");
        var body = await response.Content.ReadFromJsonAsync<ProjectDto[]>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Empty(body);
    }

    [Fact]
    public async Task GetProjects_ReturnsProjectList_WhenProjectsExist()
    {
        _factory.ProjectRepoMock
            .Setup(r => r.ListByUserAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { MakeProject() });

        var client = CreateAuthenticatedClient();
        var response = await client.GetAsync("/v1/projects");
        var body = await response.Content.ReadFromJsonAsync<ProjectDto[]>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Single(body);
        Assert.Equal("Test Project", body[0].Name);
    }

    // ─── GET /v1/projects/{id} ───────────────────────────────────────────────

    [Fact]
    public async Task GetProject_ReturnsProject_WhenExists()
    {
        _factory.ProjectRepoMock
            .Setup(r => r.GetByIdAsync(It.IsAny<string>(), "proj-001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeProject());

        var client = CreateAuthenticatedClient();
        var response = await client.GetAsync("/v1/projects/proj-001");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ProjectDto>();
        Assert.NotNull(body);
        Assert.Equal("proj-001", body.ProjectId);
    }

    [Fact]
    public async Task GetProject_IncludesAllowedWorkItemTypesField_InResponse()
    {
        // AC-010: GET response includes the allowedWorkItemTypes field
        var project = MakeProject();
        project.AllowedWorkItemTypes = new[] { "Story", "Bug" };
        _factory.ProjectRepoMock
            .Setup(r => r.GetByIdAsync(It.IsAny<string>(), "proj-001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(project);

        var client = CreateAuthenticatedClient();
        var response = await client.GetAsync("/v1/projects/proj-001");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ProjectDto>();
        Assert.NotNull(body);
        Assert.Equal(new[] { "Story", "Bug" }, body.AllowedWorkItemTypes);
    }

    [Fact]
    public async Task GetProject_Returns404_WhenNotFound()
    {
        _factory.ProjectRepoMock
            .Setup(r => r.GetByIdAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Project?)null);

        var client = CreateAuthenticatedClient();
        var response = await client.GetAsync("/v1/projects/does-not-exist");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ─── POST /v1/projects ───────────────────────────────────────────────────

    [Fact]
    public async Task CreateProject_Returns201_WithNewProject()
    {
        _factory.ProjectRepoMock
            .Setup(r => r.CreateAsync(It.IsAny<Project>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Project p, CancellationToken _) => p);

        var client = CreateAuthenticatedClient();
        var request = new CreateProjectRequest("New Project", "https://new.example.com", "Smoke tests.");
        var response = await client.PostAsJsonAsync("/v1/projects", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ProjectDto>();
        Assert.NotNull(body);
        Assert.Equal("New Project", body.Name);
        Assert.NotEmpty(body.ProjectId);
    }

    [Fact]
    public async Task CreateProject_Returns400_WhenNameMissing()
    {
        var client = CreateAuthenticatedClient();
        var payload = new { productUrl = "https://example.com", testingStrategy = "Smoke tests." };
        var response = await client.PostAsJsonAsync("/v1/projects", payload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateProject_Returns400_WhenUrlInvalid()
    {
        var client = CreateAuthenticatedClient();
        var payload = new { name = "Test", productUrl = "not-a-url", testingStrategy = "Smoke tests." };
        var response = await client.PostAsJsonAsync("/v1/projects", payload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ─── PUT /v1/projects/{id} ───────────────────────────────────────────────

    [Fact]
    public async Task UpdateProject_Returns200_WithUpdatedProject()
    {
        var existing = MakeProject();
        _factory.ProjectRepoMock
            .Setup(r => r.GetByProjectIdAsync("proj-001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        _factory.ProjectRepoMock
            .Setup(r => r.GetByIdAsync(It.IsAny<string>(), "proj-001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        _factory.ProjectRepoMock
            .Setup(r => r.UpdateAsync(It.IsAny<Project>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Project p, CancellationToken _) => p);

        var client = CreateAuthenticatedClient();
        var request = new UpdateProjectRequest("Updated Name", "https://updated.example.com", "E2E focus.");
        var response = await client.PutAsJsonAsync("/v1/projects/proj-001", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ProjectDto>();
        Assert.NotNull(body);
        Assert.Equal("Updated Name", body.Name);
    }

    [Fact]
    public async Task UpdateProject_Returns404_WhenNotFound()
    {
        _factory.ProjectRepoMock
            .Setup(r => r.GetByProjectIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Project?)null);

        var client = CreateAuthenticatedClient();
        var request = new UpdateProjectRequest("X", "https://x.com", "Y");
        var response = await client.PutAsJsonAsync("/v1/projects/does-not-exist", request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateProject_Returns403_WhenProjectBelongsToDifferentUser()
    {
        var otherUserProject = MakeProject(userId: "other-user-oid");
        _factory.ProjectRepoMock
            .Setup(r => r.GetByProjectIdAsync("proj-001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(otherUserProject);

        var client = CreateAuthenticatedClient();
        var request = new UpdateProjectRequest("X", "https://x.com", "Y");
        var response = await client.PutAsJsonAsync("/v1/projects/proj-001", request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ─── DELETE /v1/projects/{id} ────────────────────────────────────────────

    [Fact]
    public async Task DeleteProject_Returns204_WhenDeleted()
    {
        var existing = MakeProject();
        _factory.ProjectRepoMock
            .Setup(r => r.GetByProjectIdAsync("proj-001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        _factory.ProjectRepoMock
            .Setup(r => r.GetByIdAsync(It.IsAny<string>(), "proj-001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        _factory.ProjectRepoMock
            .Setup(r => r.UpdateAsync(It.IsAny<Project>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Project p, CancellationToken _) => p);

        var client = CreateAuthenticatedClient();
        var response = await client.DeleteAsync("/v1/projects/proj-001");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task DeleteProject_Returns404_WhenNotFound()
    {
        _factory.ProjectRepoMock
            .Setup(r => r.GetByProjectIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Project?)null);

        var client = CreateAuthenticatedClient();
        var response = await client.DeleteAsync("/v1/projects/does-not-exist");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteProject_Returns403_WhenProjectBelongsToDifferentUser()
    {
        var otherUserProject = MakeProject(userId: "other-user-oid");
        _factory.ProjectRepoMock
            .Setup(r => r.GetByProjectIdAsync("proj-001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(otherUserProject);

        var client = CreateAuthenticatedClient();
        var response = await client.DeleteAsync("/v1/projects/proj-001");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ─── PATCH /v1/projects/{id}/work-item-type-filter ───────────────────────

    [Fact]
    public async Task PatchWorkItemTypeFilter_Returns200_WithUpdatedProject()
    {
        var existing = MakeProject();
        _factory.ProjectRepoMock
            .Setup(r => r.GetByProjectIdAsync("proj-001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        _factory.ProjectRepoMock
            .Setup(r => r.GetByIdAsync(It.IsAny<string>(), "proj-001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        _factory.ProjectRepoMock
            .Setup(r => r.UpdateAsync(It.IsAny<Project>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Project p, CancellationToken _) => p);

        var client = CreateAuthenticatedClient();
        var payload = new { allowedWorkItemTypes = new[] { "Story", "Bug" } };
        var response = await client.PatchAsJsonAsync("/v1/projects/proj-001/work-item-type-filter", payload);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ProjectDto>();
        Assert.NotNull(body);
        Assert.Equal(new[] { "Story", "Bug" }, body.AllowedWorkItemTypes);
    }

    [Fact]
    public async Task PatchWorkItemTypeFilter_Returns400_WhenEmptyArray()
    {
        var client = CreateAuthenticatedClient();
        var payload = new { allowedWorkItemTypes = Array.Empty<string>() };
        var response = await client.PatchAsJsonAsync("/v1/projects/proj-001/work-item-type-filter", payload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var body = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(jsonOptions);
        Assert.NotNull(body);
        Assert.Contains("AllowedWorkItemTypes", body.Errors.Keys, StringComparer.OrdinalIgnoreCase);
        var messages = body.Errors.First(kv => kv.Key.Equals("AllowedWorkItemTypes", StringComparison.OrdinalIgnoreCase)).Value;
        Assert.Contains(messages, m => m.Contains("At least one work item type must be selected"));
    }

    [Fact]
    public async Task PatchWorkItemTypeFilter_Returns400_WhenArrayContainsEmptyString()
    {
        var client = CreateAuthenticatedClient();
        var payload = new { allowedWorkItemTypes = new[] { "Story", "" } };
        var response = await client.PatchAsJsonAsync("/v1/projects/proj-001/work-item-type-filter", payload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var body = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(jsonOptions);
        Assert.NotNull(body);
        Assert.Contains("AllowedWorkItemTypes", body.Errors.Keys, StringComparer.OrdinalIgnoreCase);
        var messages = body.Errors.First(kv => kv.Key.Equals("AllowedWorkItemTypes", StringComparison.OrdinalIgnoreCase)).Value;
        Assert.Contains(messages, m => m.Contains("Work item type values must be non-empty strings"));
    }

    [Fact]
    public async Task PatchWorkItemTypeFilter_Returns400_WhenMoreThan20Types()
    {
        var client = CreateAuthenticatedClient();
        var payload = new { allowedWorkItemTypes = Enumerable.Range(1, 21).Select(i => $"Type{i}").ToArray() };
        var response = await client.PatchAsJsonAsync("/v1/projects/proj-001/work-item-type-filter", payload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var body = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(jsonOptions);
        Assert.NotNull(body);
        Assert.Contains("AllowedWorkItemTypes", body.Errors.Keys, StringComparer.OrdinalIgnoreCase);
        var messages = body.Errors.First(kv => kv.Key.Equals("AllowedWorkItemTypes", StringComparison.OrdinalIgnoreCase)).Value;
        Assert.Contains(messages, m => m.Contains("A maximum of 20 work item types may be configured"));
    }

    [Fact]
    public async Task PatchWorkItemTypeFilter_Returns403_WhenProjectBelongsToDifferentUser()
    {
        var otherUserProject = MakeProject(userId: "other-user-oid");
        _factory.ProjectRepoMock
            .Setup(r => r.GetByProjectIdAsync("proj-001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(otherUserProject);

        var client = CreateAuthenticatedClient();
        var payload = new { allowedWorkItemTypes = new[] { "Story" } };
        var response = await client.PatchAsJsonAsync("/v1/projects/proj-001/work-item-type-filter", payload);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ─── RequestTimeoutSeconds validation — feature 0022 ────────────────────

    [Fact]
    public async Task UpdateProject_Returns200_AndPersistsRequestTimeoutSeconds()
    {
        // AC-004, AC-005: valid requestTimeoutSeconds value is persisted and returned
        var existing = MakeProject();
        _factory.ProjectRepoMock
            .Setup(r => r.GetByProjectIdAsync("proj-001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        _factory.ProjectRepoMock
            .Setup(r => r.GetByIdAsync(It.IsAny<string>(), "proj-001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        _factory.ProjectRepoMock
            .Setup(r => r.UpdateAsync(It.IsAny<Project>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Project p, CancellationToken _) => p);

        var client = CreateAuthenticatedClient();
        var request = new UpdateProjectRequest("My App", "https://app.example.com", "Smoke.", null, RequestTimeoutSeconds: 60);
        var response = await client.PutAsJsonAsync("/v1/projects/proj-001", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ProjectDto>();
        Assert.NotNull(body);
        Assert.Equal(60, body!.RequestTimeoutSeconds);
    }

    [Fact]
    public async Task UpdateProject_Returns400_WhenRequestTimeoutSeconds_IsBelowMinimum()
    {
        // AC-020: value below 5 returns 400 Bad Request with ValidationProblemDetails
        var client = CreateAuthenticatedClient();
        var payload = new { name = "My App", productUrl = "https://app.example.com", testingStrategy = "Smoke.", requestTimeoutSeconds = 4 };
        var response = await client.PutAsJsonAsync("/v1/projects/proj-001", payload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var body = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(jsonOptions);
        Assert.NotNull(body);
        Assert.Contains("RequestTimeoutSeconds", body!.Errors.Keys, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UpdateProject_Returns400_WhenRequestTimeoutSeconds_IsAboveMaximum()
    {
        // AC-020: value above 120 returns 400 Bad Request with ValidationProblemDetails
        var client = CreateAuthenticatedClient();
        var payload = new { name = "My App", productUrl = "https://app.example.com", testingStrategy = "Smoke.", requestTimeoutSeconds = 121 };
        var response = await client.PutAsJsonAsync("/v1/projects/proj-001", payload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var body = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(jsonOptions);
        Assert.NotNull(body);
        Assert.Contains("RequestTimeoutSeconds", body!.Errors.Keys, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UpdateProject_ReturnsDefault30_WhenRequestTimeoutSeconds_IsOmitted()
    {
        // AC-006, AC-009: when the field is omitted the response should include 30
        var existing = MakeProject();
        _factory.ProjectRepoMock
            .Setup(r => r.GetByProjectIdAsync("proj-001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        _factory.ProjectRepoMock
            .Setup(r => r.GetByIdAsync(It.IsAny<string>(), "proj-001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        _factory.ProjectRepoMock
            .Setup(r => r.UpdateAsync(It.IsAny<Project>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Project p, CancellationToken _) => p);

        var client = CreateAuthenticatedClient();
        // Omit requestTimeoutSeconds — the DTO default (30) will be used
        var payload = new { name = "My App", productUrl = "https://app.example.com", testingStrategy = "Smoke." };
        var response = await client.PutAsJsonAsync("/v1/projects/proj-001", payload);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ProjectDto>();
        Assert.NotNull(body);
        Assert.Equal(30, body!.RequestTimeoutSeconds);
    }

    [Fact]
    public async Task GetProject_IncludesRequestTimeoutSeconds_InResponse()
    {
        // AC-008: GET /api/projects/{id} includes requestTimeoutSeconds in the response body
        var project = MakeProject();
        project.RequestTimeoutSeconds = 45;
        _factory.ProjectRepoMock
            .Setup(r => r.GetByIdAsync(It.IsAny<string>(), "proj-001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(project);

        var client = CreateAuthenticatedClient();
        var response = await client.GetAsync("/v1/projects/proj-001");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ProjectDto>();
        Assert.NotNull(body);
        Assert.Equal(45, body!.RequestTimeoutSeconds);
    }

    // ─── Auth guard ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GetProjects_Returns401_WithoutAuthToken()
    {
        var client = _factory.CreateClient();  // no Authorization header
        var response = await client.GetAsync("/v1/projects");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
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
                    ["AzureAdB2C:ClientId"] = "test-client-id"
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

                // Replace JWT authentication with a test scheme that reads the OID claim
                // from a simple Base64-encoded JSON payload so endpoints can extract userId.
                services.AddAuthentication("Test")
                    .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions,
                        TestAuthHandler>("Test", _ => { });
            });
        }
    }
}

/// <summary>
/// Minimal authentication handler for integration tests.
/// Accepts any Bearer token and returns a ClaimsPrincipal with a fixed OID claim
/// matching the userId used in test project fixtures.
/// </summary>
internal sealed class TestAuthHandler(
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
