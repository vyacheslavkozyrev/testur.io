using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using Testurio.Api.DTOs;
using Testurio.Api.Services;
using Testurio.Core.Entities;
using Testurio.Core.Interfaces;
using Testurio.Core.Repositories;
using Testurio.Infrastructure;

namespace Testurio.IntegrationTests.Controllers;

/// <summary>
/// Integration tests for POST /v1/projects/{projectId}/prompt-check.
/// </summary>
public class ProjectPromptCheckControllerTests : IClassFixture<ProjectPromptCheckControllerTests.ApiFactory>
{
    private readonly ApiFactory _factory;

    public ProjectPromptCheckControllerTests(ApiFactory factory)
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

    private static PromptCheckFeedback MakeFeedback() => new(
        Clarity: new PromptCheckDimension("Clear and concise.", null),
        Specificity: new PromptCheckDimension("Specific.", "Add more detail."),
        PotentialConflicts: new PromptCheckDimension("No conflicts.", null));

    private HttpClient CreateAuthenticatedClient()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "test-token");
        return client;
    }

    // ─── POST /v1/projects/{id}/prompt-check — success ───────────────────────

    [Fact]
    public async Task PromptCheck_Returns200_WithFeedback_WhenProjectOwnedByUser()
    {
        var project = MakeProject();
        _factory.ProjectRepoMock
            .Setup(r => r.GetByProjectIdAsync("proj-001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(project);
        _factory.ProjectRepoMock
            .Setup(r => r.GetByIdAsync(It.IsAny<string>(), "proj-001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(project);
        _factory.PromptCheckServiceMock
            .Setup(s => s.CheckAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeFeedback());

        var client = CreateAuthenticatedClient();
        var request = new PromptCheckRequest("Always test with expired tokens.");
        var response = await client.PostAsJsonAsync("/v1/projects/proj-001/prompt-check", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PromptCheckFeedback>();
        Assert.NotNull(body);
        Assert.Equal("Clear and concise.", body.Clarity.Assessment);
    }

    // ─── POST /v1/projects/{id}/prompt-check — 404 ───────────────────────────

    [Fact]
    public async Task PromptCheck_Returns404_WhenProjectDoesNotExist()
    {
        _factory.ProjectRepoMock
            .Setup(r => r.GetByProjectIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Project?)null);

        var client = CreateAuthenticatedClient();
        var request = new PromptCheckRequest("Some prompt.");
        var response = await client.PostAsJsonAsync("/v1/projects/does-not-exist/prompt-check", request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ─── POST /v1/projects/{id}/prompt-check — 403 ───────────────────────────

    [Fact]
    public async Task PromptCheck_Returns403_WhenProjectBelongsToDifferentUser()
    {
        var otherUserProject = MakeProject(userId: "other-user-oid");
        _factory.ProjectRepoMock
            .Setup(r => r.GetByProjectIdAsync("proj-001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(otherUserProject);

        var client = CreateAuthenticatedClient();
        var request = new PromptCheckRequest("Some prompt.");
        var response = await client.PostAsJsonAsync("/v1/projects/proj-001/prompt-check", request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ─── POST /v1/projects/{id}/prompt-check — 400 ───────────────────────────

    [Fact]
    public async Task PromptCheck_Returns400_WhenCustomPromptIsEmpty()
    {
        var client = CreateAuthenticatedClient();
        var request = new { customPrompt = "" };
        var response = await client.PostAsJsonAsync("/v1/projects/proj-001/prompt-check", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PromptCheck_Returns400_WhenCustomPromptExceeds5000Characters()
    {
        var client = CreateAuthenticatedClient();
        var request = new { customPrompt = new string('A', 5001) };
        var response = await client.PostAsJsonAsync("/v1/projects/proj-001/prompt-check", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ─── POST /v1/projects/{id}/prompt-check — 401 ───────────────────────────

    [Fact]
    public async Task PromptCheck_Returns401_WithoutAuthToken()
    {
        var client = _factory.CreateClient();
        var request = new PromptCheckRequest("Some prompt.");
        var response = await client.PostAsJsonAsync("/v1/projects/proj-001/prompt-check", request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    public class ApiFactory : WebApplicationFactory<Program>
    {
        private readonly Mock<IProjectRepository> _projectRepo = new();
        private readonly Mock<ITestRunRepository> _testRunRepo = new();
        private readonly Mock<IRunQueueRepository> _runQueueRepo = new();
        private readonly Mock<ITestRunJobSender> _jobSender = new();
        private readonly Mock<IJiraApiClient> _jiraApiClient = new();
        private readonly Mock<IPromptCheckService> _promptCheckService = new();

        public Mock<IProjectRepository> ProjectRepoMock => _projectRepo;
        public Mock<IPromptCheckService> PromptCheckServiceMock => _promptCheckService;

        public void ResetMocks()
        {
            _projectRepo.Reset();
            _testRunRepo.Reset();
            _runQueueRepo.Reset();
            _jobSender.Reset();
            _jiraApiClient.Reset();
            _promptCheckService.Reset();
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
                services.Replace(ServiceDescriptor.Scoped<IPromptCheckService>(_ => _promptCheckService.Object));

                services.AddAuthentication("Test")
                    .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions,
                        TestAuthHandler>("Test", _ => { });
            });
        }
    }
}
