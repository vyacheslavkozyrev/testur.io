using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Moq;
using Testurio.Api.DTOs;
using Testurio.Api.Services;
using Testurio.Core.Entities;
using Testurio.Core.Enums;
using Testurio.Core.Events;
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

    /// <summary>
    /// Matches the API's <c>JsonStringEnumConverter</c> registration so enum values
    /// round-trip correctly during deserialization in tests.
    /// </summary>
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

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
        var body = await response.Content.ReadFromJsonAsync<DashboardResponse>(JsonOptions);

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
        var body = await response.Content.ReadFromJsonAsync<DashboardResponse>(JsonOptions);

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

    // ─── GET /v1/stats/projects/{projectId}/history ───────────────────────────

    [Fact]
    public async Task GetProjectHistory_Returns401_WithoutAuthToken()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync($"/v1/stats/projects/{Guid.NewGuid()}/history");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetProjectHistory_Returns404_WhenProjectBelongsToDifferentUser()
    {
        var projectId = Guid.NewGuid().ToString();
        _factory.StatsRepoMock
            .Setup(r => r.GetProjectHistoryAsync("test-user-oid", projectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(default((IReadOnlyList<RunHistoryItem> Runs, IReadOnlyList<TrendPoint> TrendPoints)?));

        var client = CreateAuthenticatedClient();
        var response = await client.GetAsync($"/v1/stats/projects/{projectId}/history");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetProjectHistory_Returns200_WithRunListAnd90TrendPoints()
    {
        var projectId = Guid.NewGuid().ToString();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var runs = (IReadOnlyList<RunHistoryItem>) new[]
        {
            new RunHistoryItem("r1", "run-1", "Story A", "PASSED", "approve", 2, 2, 0, 0, 3000, DateTimeOffset.UtcNow),
        };

        var trendPoints = Enumerable.Range(0, 90)
            .Select(i => new TrendPoint(today.AddDays(-(89 - i)), 0, 0))
            .ToArray() as IReadOnlyList<TrendPoint>;

        _factory.StatsRepoMock
            .Setup(r => r.GetProjectHistoryAsync("test-user-oid", projectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((runs, trendPoints));

        var client = CreateAuthenticatedClient();
        var response = await client.GetAsync($"/v1/stats/projects/{projectId}/history");
        var body = await response.Content.ReadFromJsonAsync<ProjectHistoryResponse>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Single(body.Runs);
        Assert.Equal(90, body.TrendPoints.Count);
    }

    [Fact]
    public async Task GetProjectHistory_IsDeletedRecords_NotIncluded()
    {
        // Repository-level filtering is verified via the repository mock returning only active records.
        var projectId = Guid.NewGuid().ToString();
        var runs = (IReadOnlyList<RunHistoryItem>) Array.Empty<RunHistoryItem>();
        var trendPoints = (IReadOnlyList<TrendPoint>) Array.Empty<TrendPoint>();

        _factory.StatsRepoMock
            .Setup(r => r.GetProjectHistoryAsync("test-user-oid", projectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((runs, trendPoints));

        var client = CreateAuthenticatedClient();
        var response = await client.GetAsync($"/v1/stats/projects/{projectId}/history");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ─── GET /v1/stats/projects/{projectId}/runs/{runId} ─────────────────────

    [Fact]
    public async Task GetRunDetail_Returns401_WithoutAuthToken()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync($"/v1/stats/projects/{Guid.NewGuid()}/runs/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetRunDetail_Returns404_WhenRunNotFound()
    {
        var projectId = Guid.NewGuid().ToString();
        var runId = Guid.NewGuid().ToString();

        _factory.StatsRepoMock
            .Setup(r => r.GetRunDetailAsync("test-user-oid", projectId, runId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TestResult?) null);

        var client = CreateAuthenticatedClient();
        var response = await client.GetAsync($"/v1/stats/projects/{projectId}/runs/{runId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetRunDetail_Returns200_WithCorrectDetail()
    {
        var projectId = Guid.NewGuid().ToString();
        var runId = Guid.NewGuid().ToString();

        var testResult = new TestResult
        {
            Id = "result-1",
            RunId = runId,
            ProjectId = projectId,
            UserId = "test-user-oid",
            StoryTitle = "User can log in",
            Verdict = "PASSED",
            Recommendation = "approve",
            TotalDurationMs = 7000,
            ScenarioResults = new[]
            {
                new ScenarioSummary("scenario-1", "POST /auth — 200", true, 400, null, "api", Array.Empty<string>()),
            },
            RawCommentMarkdown = "## Report\nPASSED",
        };

        _factory.StatsRepoMock
            .Setup(r => r.GetRunDetailAsync("test-user-oid", projectId, runId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(testResult);

        var client = CreateAuthenticatedClient();
        var response = await client.GetAsync($"/v1/stats/projects/{projectId}/runs/{runId}");
        var body = await response.Content.ReadFromJsonAsync<RunDetailResponse>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal("result-1", body.Id);
        Assert.Equal("PASSED", body.Verdict);
        Assert.Single(body.ScenarioResults);
        Assert.Equal("## Report\nPASSED", body.RawCommentMarkdown);
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
        var body = await response.Content.ReadFromJsonAsync<DashboardResponse>(JsonOptions);

        Assert.NotNull(body);
        Assert.Equal(3, body.Projects.Count);
        Assert.Equal("proj-newer", body.Projects[0].ProjectId);
        Assert.Equal("proj-older", body.Projects[1].ProjectId);
        Assert.Equal("proj-norun", body.Projects[2].ProjectId);
    }

    // ─── GET /v1/stats/dashboard/stream — SSE ────────────────────────────────

    [Fact]
    public async Task StreamDashboard_Returns401_WithoutAuthToken()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/v1/stats/dashboard/stream");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task StreamDashboard_ReceivesFirstSseDataLine_AfterPublishAsync()
    {
        var client = CreateAuthenticatedClient();

        // Start reading the SSE stream in the background.
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var responseTask = client.GetAsync(
            "/v1/stats/dashboard/stream",
            HttpCompletionOption.ResponseHeadersRead,
            cts.Token);

        // Give the server time to open the connection and register the channel.
        await Task.Delay(200, cts.Token);

        // Publish an event via the in-process IDashboardStreamManager.
        var streamManager = _factory.Services.GetRequiredService<IDashboardStreamManager>();
        var @event = new DashboardUpdatedEvent(
            "proj-sse-test",
            new LatestRunSummary("run-sse", RunStatus.Passed, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow),
            null);

        await streamManager.PublishAsync("test-user-oid", @event, cts.Token);

        var response = await responseTask;
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/event-stream", response.Content.Headers.ContentType?.MediaType);

        // Read until the first data line arrives.
        await using var stream = await response.Content.ReadAsStreamAsync(cts.Token);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        string? line = null;
        while (!cts.IsCancellationRequested)
        {
            line = await reader.ReadLineAsync(cts.Token);
            if (line is not null && line.StartsWith("data:", StringComparison.Ordinal))
                break;
        }

        Assert.NotNull(line);
        Assert.StartsWith("data:", line, StringComparison.Ordinal);

        var json = line["data: ".Length..];
        using var doc = JsonDocument.Parse(json);
        Assert.Equal("proj-sse-test", doc.RootElement.GetProperty("projectId").GetString());
    }

    [Fact]
    public async Task StreamDashboard_ClosesCleanly_WhenClientCancels()
    {
        var client = CreateAuthenticatedClient();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        var response = await client.GetAsync(
            "/v1/stats/dashboard/stream",
            HttpCompletionOption.ResponseHeadersRead,
            cts.Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Cancel the connection — should not throw on the client side.
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await using var stream = await response.Content.ReadAsStreamAsync(CancellationToken.None);
            using var reader = new StreamReader(stream);
            // Keep reading until the cancellation propagates to the content stream.
            while (true)
                await reader.ReadLineAsync(cts.Token);
        });
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

                // Feature 0043: remove the DashboardEventRelay singleton and its companion
                // IHostedService registration so the test host does not attempt a real Service
                // Bus connection during startup.
                // Program.cs uses AddSingleton<DashboardEventRelay>(factory) +
                // AddHostedService(sp => sp.GetRequiredService<DashboardEventRelay>()) — the
                // IHostedService descriptor has a non-null ImplementationFactory and no
                // ImplementationType (unlike typed AddHostedService<T>() registrations).
                // Removing descriptors matching that shape targets only the relay; other hosted
                // services registered by the framework (e.g. generic host lifetime) use typed
                // ImplementationType descriptors and are left intact.
                services.RemoveAll<DashboardEventRelay>();
                var relayDescriptors = services
                    .Where(d =>
                        d.ServiceType == typeof(IHostedService) &&
                        d.ImplementationType is null &&
                        d.ImplementationInstance is null &&
                        d.ImplementationFactory is not null)
                    .ToList();
                foreach (var descriptor in relayDescriptors)
                    services.Remove(descriptor);

                services.AddAuthentication("Test")
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", _ => { });
            });
        }
    }
}
