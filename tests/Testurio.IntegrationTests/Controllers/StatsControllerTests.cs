using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using Testurio.Api.DTOs;
using Testurio.Core.Enums;
using Testurio.Core.Interfaces;
using Testurio.Core.Models;
using Testurio.Infrastructure;

namespace Testurio.IntegrationTests.Controllers;

/// <summary>
/// Integration tests for <c>GET /v1/stats/dashboard</c>.
/// Uses a mock <see cref="IStatsRepository"/> so no Cosmos emulator is required.
/// </summary>
public class StatsControllerTests : IClassFixture<StatsControllerTests.ApiFactory>
{
    private readonly ApiFactory _factory;

    public StatsControllerTests(ApiFactory factory)
    {
        _factory = factory;
        _factory.ResetMocks();
    }

    private HttpClient CreateAuthenticatedClient()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "test-token");
        return client;
    }

    private static DashboardProjectSummary MakeProject(
        string projectId = "proj-001",
        string name = "Test Project",
        LatestRunSummary? latestRun = null) =>
        new(projectId, name, "https://example.com", "API testing.", latestRun);

    private static LatestRunSummary MakeRun(RunStatus status = RunStatus.Passed) =>
        new("run-001", status, DateTimeOffset.UtcNow.AddHours(-1), DateTimeOffset.UtcNow);

    private static QuotaUsage DefaultQuota() =>
        new(3, 50, DateTimeOffset.UtcNow.Date.AddDays(1));

    // ─── GET /v1/stats/dashboard — auth guard ─────────────────────────────────

    [Fact]
    public async Task GetDashboard_Returns401_WithoutAuthToken()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/v1/stats/dashboard");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ─── GET /v1/stats/dashboard — success scenarios ──────────────────────────

    [Fact]
    public async Task GetDashboard_Returns200_WithProjectsAndQuota()
    {
        _factory.StatsRepoMock
            .Setup(r => r.GetDashboardSummariesAsync("test-user-oid", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { MakeProject(latestRun: MakeRun()) });
        _factory.StatsRepoMock
            .Setup(r => r.GetQuotaUsageAsync("test-user-oid", It.IsAny<CancellationToken>()))
            .ReturnsAsync(DefaultQuota());

        var client = CreateAuthenticatedClient();
        var response = await client.GetAsync("/v1/stats/dashboard");
        var body = await response.Content.ReadFromJsonAsync<DashboardResponse>(
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Single(body.Projects);
        Assert.Equal("proj-001", body.Projects[0].ProjectId);
        Assert.NotNull(body.QuotaUsage);
        Assert.Equal(3, body.QuotaUsage.UsedToday);
    }

    [Fact]
    public async Task GetDashboard_Returns200_WithEmptyProjectsForNewUser()
    {
        _factory.StatsRepoMock
            .Setup(r => r.GetDashboardSummariesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<DashboardProjectSummary>());
        _factory.StatsRepoMock
            .Setup(r => r.GetQuotaUsageAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(DefaultQuota());

        var client = CreateAuthenticatedClient();
        var response = await client.GetAsync("/v1/stats/dashboard");
        var body = await response.Content.ReadFromJsonAsync<DashboardResponse>(
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Empty(body.Projects);
    }

    [Fact]
    public async Task GetDashboard_UsesUserIdFromToken_NotFromQueryString()
    {
        // The repository is set up to return data only for "test-user-oid" (the fixed OID in TestAuthHandler).
        // Passing a different userId via query string must be ignored by the server.
        _factory.StatsRepoMock
            .Setup(r => r.GetDashboardSummariesAsync("test-user-oid", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { MakeProject() });
        _factory.StatsRepoMock
            .Setup(r => r.GetQuotaUsageAsync("test-user-oid", It.IsAny<CancellationToken>()))
            .ReturnsAsync(DefaultQuota());

        var client = CreateAuthenticatedClient();
        // Attempt to supply an attacker-controlled userId in the query string.
        var response = await client.GetAsync("/v1/stats/dashboard?userId=attacker-oid");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        // The endpoint must have called the repo with the token-derived userId only.
        _factory.StatsRepoMock.Verify(
            r => r.GetDashboardSummariesAsync("test-user-oid", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetDashboard_SortOrder_RunsPresentFirstByStartedAtDesc()
    {
        var older = DateTimeOffset.UtcNow.AddHours(-3);
        var newer = DateTimeOffset.UtcNow.AddHours(-1);

        var projects = new[]
        {
            MakeProject("proj-newer", "Newer", new LatestRunSummary("r2", RunStatus.Passed, newer, null)),
            MakeProject("proj-older", "Older", new LatestRunSummary("r1", RunStatus.Failed, older, null)),
            MakeProject("proj-norun", "No Run"),
        };

        _factory.StatsRepoMock
            .Setup(r => r.GetDashboardSummariesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(projects);
        _factory.StatsRepoMock
            .Setup(r => r.GetQuotaUsageAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(DefaultQuota());

        var client = CreateAuthenticatedClient();
        var response = await client.GetAsync("/v1/stats/dashboard");
        var body = await response.Content.ReadFromJsonAsync<DashboardResponse>(
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(body);
        Assert.Equal(3, body.Projects.Count);
        Assert.Equal("proj-newer", body.Projects[0].ProjectId);
        Assert.Equal("proj-older", body.Projects[1].ProjectId);
        Assert.Equal("proj-norun", body.Projects[2].ProjectId);
    }

    public class ApiFactory : WebApplicationFactory<Program>
    {
        private readonly Mock<IStatsRepository> _statsRepo = new();

        public Mock<IStatsRepository> StatsRepoMock => _statsRepo;

        public void ResetMocks() => _statsRepo.Reset();

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
                services.Replace(ServiceDescriptor.Singleton<IStatsRepository>(_ => _statsRepo.Object));

                services.AddAuthentication("Test")
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", _ => { });
            });
        }
    }
}
