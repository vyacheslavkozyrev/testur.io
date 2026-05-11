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
using Testurio.Core.Enums;
using Testurio.Core.Interfaces;
using Testurio.Core.Repositories;
using Testurio.Infrastructure;

namespace Testurio.IntegrationTests.Controllers;

public class PMToolIntegrationTests : IClassFixture<PMToolIntegrationTests.ApiFactory>
{
    private readonly ApiFactory _factory;

    public PMToolIntegrationTests(ApiFactory factory)
    {
        _factory = factory;
        _factory.ResetMocks();
    }

    private static Project MakeProject(string userId = "test-user-oid", string projectId = "proj-007") => new()
    {
        Id = projectId,
        UserId = userId,
        Name = "Integration Test Project",
        ProductUrl = "https://app.example.com",
        TestingStrategy = "API smoke tests.",
    };

    private static Project MakeAdoProject(string userId = "test-user-oid") => new()
    {
        Id = "proj-007",
        UserId = userId,
        Name = "ADO Project",
        ProductUrl = "https://app.example.com",
        TestingStrategy = "API smoke tests.",
        PmTool = PMToolType.Ado,
        IntegrationStatus = IntegrationStatus.Active,
        AdoOrgUrl = "https://dev.azure.com/myorg",
        AdoProjectName = "My Project",
        AdoTeam = "My Team",
        AdoInTestingStatus = "In Testing",
        AdoAuthMethod = ADOAuthMethod.Pat,
        AdoTokenSecretUri = "projects--proj-007--adoToken",
        WebhookSecretUri = "projects--proj-007--webhookSecret",
    };

    private HttpClient CreateAuthenticatedClient()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "test-token");
        return client;
    }

    // ─── GET /v1/projects/{id}/integrations ──────────────────────────────────

    [Fact]
    public async Task GetIntegrationStatus_ReturnsNone_WhenNotConfigured()
    {
        var project = MakeProject();
        _factory.ProjectRepoMock
            .Setup(r => r.GetByProjectIdAsync("proj-007", It.IsAny<CancellationToken>()))
            .ReturnsAsync(project);
        _factory.ProjectRepoMock
            .Setup(r => r.GetByIdAsync("test-user-oid", "proj-007", It.IsAny<CancellationToken>()))
            .ReturnsAsync(project);

        var client = CreateAuthenticatedClient();
        var response = await client.GetAsync("/v1/projects/proj-007/integrations");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PMToolConnectionResponse>();
        Assert.NotNull(body);
        Assert.Null(body!.PmTool);
    }

    [Fact]
    public async Task GetIntegrationStatus_Returns403_WhenProjectBelongsToDifferentUser()
    {
        var otherProject = MakeProject(userId: "other-user-oid");
        _factory.ProjectRepoMock
            .Setup(r => r.GetByProjectIdAsync("proj-007", It.IsAny<CancellationToken>()))
            .ReturnsAsync(otherProject);

        var client = CreateAuthenticatedClient();
        var response = await client.GetAsync("/v1/projects/proj-007/integrations");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetIntegrationStatus_Returns401_WithoutAuthToken()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/v1/projects/proj-007/integrations");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ─── POST /v1/projects/{id}/integrations/ado ─────────────────────────────

    [Fact]
    public async Task SaveADOConnection_Returns200_WhenValid()
    {
        var project = MakeProject();
        _factory.ProjectRepoMock
            .Setup(r => r.GetByProjectIdAsync("proj-007", It.IsAny<CancellationToken>()))
            .ReturnsAsync(project);
        _factory.ProjectRepoMock
            .Setup(r => r.GetByIdAsync("test-user-oid", "proj-007", It.IsAny<CancellationToken>()))
            .ReturnsAsync(project);
        _factory.ProjectRepoMock
            .Setup(r => r.UpdateAsync(It.IsAny<Project>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Project p, CancellationToken _) => p);

        var client = CreateAuthenticatedClient();
        var request = new SaveADOConnectionRequest(
            "https://dev.azure.com/myorg", "My Project", "My Team",
            "In Testing", ADOAuthMethod.Pat, "my-pat", null);

        var response = await client.PostAsJsonAsync("/v1/projects/proj-007/integrations/ado", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PMToolConnectionResponse>();
        Assert.NotNull(body);
        Assert.Equal("ado", body!.PmTool?.ToString().ToLowerInvariant());
    }

    [Fact]
    public async Task SaveADOConnection_Returns400_WhenOrgUrlInvalid()
    {
        var client = CreateAuthenticatedClient();
        var request = new SaveADOConnectionRequest(
            "not-a-url", "My Project", "My Team",
            "In Testing", ADOAuthMethod.Pat, "my-pat", null);

        var response = await client.PostAsJsonAsync("/v1/projects/proj-007/integrations/ado", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SaveADOConnection_Returns400_WhenPatMissing()
    {
        var client = CreateAuthenticatedClient();
        // OrgUrl is valid but PAT auth method has no PAT
        var payload = new
        {
            orgUrl = "https://dev.azure.com/myorg",
            projectName = "My Project",
            team = "My Team",
            inTestingStatus = "In Testing",
            authMethod = 0, // Pat = 0
            pat = (string?)null
        };

        var response = await client.PostAsJsonAsync("/v1/projects/proj-007/integrations/ado", payload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ─── POST /v1/projects/{id}/integrations/jira ─────────────────────────────

    [Fact]
    public async Task SaveJiraConnection_Returns200_WhenValid()
    {
        var project = MakeProject();
        _factory.ProjectRepoMock
            .Setup(r => r.GetByProjectIdAsync("proj-007", It.IsAny<CancellationToken>()))
            .ReturnsAsync(project);
        _factory.ProjectRepoMock
            .Setup(r => r.GetByIdAsync("test-user-oid", "proj-007", It.IsAny<CancellationToken>()))
            .ReturnsAsync(project);
        _factory.ProjectRepoMock
            .Setup(r => r.UpdateAsync(It.IsAny<Project>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Project p, CancellationToken _) => p);

        var client = CreateAuthenticatedClient();
        var request = new SaveJiraConnectionRequest(
            "https://myorg.atlassian.net", "PROJ", "In Testing",
            JiraAuthMethod.ApiToken, "user@example.com", "my-token", null);

        var response = await client.PostAsJsonAsync("/v1/projects/proj-007/integrations/jira", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PMToolConnectionResponse>();
        Assert.NotNull(body);
    }

    [Fact]
    public async Task SaveJiraConnection_Returns400_WhenBaseUrlInvalid()
    {
        var client = CreateAuthenticatedClient();
        var request = new SaveJiraConnectionRequest(
            "not-a-url", "PROJ", "In Testing",
            JiraAuthMethod.ApiToken, "user@example.com", "my-token", null);

        var response = await client.PostAsJsonAsync("/v1/projects/proj-007/integrations/jira", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ─── DELETE /v1/projects/{id}/integrations ────────────────────────────────

    [Fact]
    public async Task RemoveIntegration_Returns200_WithEmptyConfig()
    {
        var project = MakeAdoProject();
        _factory.ProjectRepoMock
            .Setup(r => r.GetByProjectIdAsync("proj-007", It.IsAny<CancellationToken>()))
            .ReturnsAsync(project);
        _factory.ProjectRepoMock
            .Setup(r => r.GetByIdAsync("test-user-oid", "proj-007", It.IsAny<CancellationToken>()))
            .ReturnsAsync(project);
        _factory.ProjectRepoMock
            .Setup(r => r.UpdateAsync(It.IsAny<Project>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Project p, CancellationToken _) => p);

        _factory.ADOClientMock
            .Setup(c => c.DeregisterWebhookAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var client = CreateAuthenticatedClient();
        var response = await client.DeleteAsync("/v1/projects/proj-007/integrations");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PMToolConnectionResponse>();
        Assert.Null(body!.PmTool);
    }

    [Fact]
    public async Task RemoveIntegration_Returns403_WhenProjectBelongsToDifferentUser()
    {
        var otherProject = MakeAdoProject(userId: "other-user-oid");
        _factory.ProjectRepoMock
            .Setup(r => r.GetByProjectIdAsync("proj-007", It.IsAny<CancellationToken>()))
            .ReturnsAsync(otherProject);

        var client = CreateAuthenticatedClient();
        var response = await client.DeleteAsync("/v1/projects/proj-007/integrations");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ─── POST /v1/projects/{id}/integrations/test-connection ─────────────────

    [Fact]
    public async Task TestConnection_Returns200_WithStructuredResult()
    {
        var project = MakeAdoProject();
        _factory.ProjectRepoMock
            .Setup(r => r.GetByProjectIdAsync("proj-007", It.IsAny<CancellationToken>()))
            .ReturnsAsync(project);
        _factory.ProjectRepoMock
            .Setup(r => r.GetByIdAsync("test-user-oid", "proj-007", It.IsAny<CancellationToken>()))
            .ReturnsAsync(project);
        _factory.ProjectRepoMock
            .Setup(r => r.UpdateAsync(It.IsAny<Project>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Project p, CancellationToken _) => p);

        _factory.ADOClientMock
            .Setup(c => c.TestConnectionAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ADOConnectionTestResult(true, 200, null));

        var client = CreateAuthenticatedClient();
        var response = await client.PostAsync("/v1/projects/proj-007/integrations/test-connection", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<TestConnectionResponse>();
        Assert.NotNull(body);
        Assert.Equal("ok", body!.Status);
    }

    // ─── GET /v1/projects/{id}/integrations/webhook-setup ────────────────────

    [Fact]
    public async Task GetWebhookSetup_Returns200_WithWebhookUrl()
    {
        var project = MakeAdoProject();
        project.WebhookSecretViewed = false;
        _factory.ProjectRepoMock
            .Setup(r => r.GetByProjectIdAsync("proj-007", It.IsAny<CancellationToken>()))
            .ReturnsAsync(project);
        _factory.ProjectRepoMock
            .Setup(r => r.GetByIdAsync("test-user-oid", "proj-007", It.IsAny<CancellationToken>()))
            .ReturnsAsync(project);
        _factory.ProjectRepoMock
            .Setup(r => r.UpdateAsync(It.IsAny<Project>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Project p, CancellationToken _) => p);

        var client = CreateAuthenticatedClient();
        var response = await client.GetAsync("/v1/projects/proj-007/integrations/webhook-setup");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<WebhookSetupResponse>();
        Assert.NotNull(body);
        Assert.Contains("/webhooks/ado", body!.WebhookUrl);
    }

    public class ApiFactory : WebApplicationFactory<Program>
    {
        private readonly Mock<IProjectRepository> _projectRepo = new();
        private readonly Mock<ITestRunRepository> _testRunRepo = new();
        private readonly Mock<IRunQueueRepository> _runQueueRepo = new();
        private readonly Mock<ITestRunJobSender> _jobSender = new();
        private readonly Mock<IJiraApiClient> _jiraApiClient = new();
        private readonly Mock<IADOClient> _adoClient = new();
        private readonly Mock<IJiraClient> _jiraClient = new();

        public Mock<IProjectRepository> ProjectRepoMock => _projectRepo;
        public Mock<IADOClient> ADOClientMock => _adoClient;
        public Mock<IJiraClient> JiraClientMock => _jiraClient;

        public void ResetMocks()
        {
            _projectRepo.Reset();
            _testRunRepo.Reset();
            _runQueueRepo.Reset();
            _jobSender.Reset();
            _jiraApiClient.Reset();
            _adoClient.Reset();
            _jiraClient.Reset();
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
                    ["AzureAdB2C:Authority"] = "https://login.microsoftonline.com/test-tenant",
                    ["AzureAdB2C:ClientId"] = "test-client-id",
                    ["PMTool:ApiBaseUrl"] = "https://api.testur.io"
                });
            });

            builder.ConfigureTestServices(services =>
            {
                services.Replace(ServiceDescriptor.Singleton<IProjectRepository>(_ => _projectRepo.Object));
                services.Replace(ServiceDescriptor.Singleton<ITestRunRepository>(_ => _testRunRepo.Object));
                services.Replace(ServiceDescriptor.Singleton<IRunQueueRepository>(_ => _runQueueRepo.Object));
                services.Replace(ServiceDescriptor.Singleton<ITestRunJobSender>(_ => _jobSender.Object));
                services.Replace(ServiceDescriptor.Singleton<IJiraApiClient>(_ => _jiraApiClient.Object));
                services.Replace(ServiceDescriptor.Singleton<IADOClient>(_ => _adoClient.Object));
                services.Replace(ServiceDescriptor.Singleton<IJiraClient>(_ => _jiraClient.Object));
                services.Replace(ServiceDescriptor.Singleton<ISecretResolver>(_ => new PassthroughSecretResolver()));

                services.AddAuthentication("Test")
                    .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions,
                        TestAuthHandler>("Test", _ => { });
            });
        }
    }
}
