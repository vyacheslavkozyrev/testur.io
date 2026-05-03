using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using Testurio.Core.Entities;
using Testurio.Core.Interfaces;
using Testurio.Core.Models;
using Testurio.Core.Repositories;
using Testurio.Infrastructure;
using Xunit;

namespace Testurio.IntegrationTests.Controllers;

public class JiraWebhookControllerTests : IClassFixture<JiraWebhookControllerTests.ApiFactory>
{
    private const string WebhookSecret = "test-secret";

    private readonly ApiFactory _factory;

    public JiraWebhookControllerTests(ApiFactory factory)
    {
        _factory = factory;
        _factory.ResetMocks();
    }

    private static string Sign(string body, string secret)
    {
        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(body));
        return $"sha256={Convert.ToHexString(hash).ToLowerInvariant()}";
    }

    private static Project MakeProject() => new()
    {
        Id = "proj1",
        UserId = "user1",
        Name = "Test Project",
        ProductUrl = "https://app.example.com",
        JiraBaseUrl = "https://example.atlassian.net",
        JiraProjectKey = "PROJ",
        JiraEmail = "qa@example.com",
        JiraApiTokenSecretRef = "token",
        JiraWebhookSecretRef = WebhookSecret,
        InTestingStatusLabel = "In Testing"
    };

    private static JiraWebhookPayload MakePayload(
        string issueType = "Story",
        string transitionTo = "In Testing",
        string? description = "A description",
        string? ac = "Given/when/then") => new()
    {
        WebhookEvent = "jira:issue_transitioned",
        Issue = new JiraIssue
        {
            Id = "10001",
            Key = "PROJ-1",
            Fields = new JiraIssueFields
            {
                IssueType = new JiraIssueType { Name = issueType },
                Status = new JiraStatus { Name = transitionTo },
                Description = description,
                AcceptanceCriteria = ac
            }
        },
        Transition = new JiraTransition { To = new JiraTransitionTo { Name = transitionTo } }
    };

    private HttpClient CreateClient() => _factory.CreateClient();

    private async Task<HttpResponseMessage> PostWebhookAsync(HttpClient client, object payload, string? signature = null)
    {
        var body = JsonSerializer.Serialize(payload);
        var sig = signature ?? Sign(body, WebhookSecret);
        var request = new HttpRequestMessage(HttpMethod.Post, "/v1/webhooks/jira/proj1");
        request.Headers.Add("X-Hub-Signature-256", sig);
        request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        return await client.SendAsync(request);
    }

    private Mock<IProjectRepository> GetProjectRepoMock() =>
        _factory.Services.GetRequiredService<Mock<IProjectRepository>>();

    [Fact]
    public async Task PostWebhook_WithMissingSignature_Returns401()
    {
        var client = CreateClient();
        var body = JsonSerializer.Serialize(MakePayload());
        var request = new HttpRequestMessage(HttpMethod.Post, "/v1/webhooks/jira/proj1");
        request.Content = new StringContent(body, Encoding.UTF8, "application/json");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PostWebhook_WithInvalidSignature_Returns401()
    {
        GetProjectRepoMock()
            .Setup(r => r.GetByProjectIdAsync("proj1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeProject());

        var client = CreateClient();
        var response = await PostWebhookAsync(client, MakePayload(), "sha256=invalidsignature0000000000000000000000000000000000000000000000000000");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PostWebhook_ValidPayloadNoActiveRun_Returns202()
    {
        var projectRepo = _factory.Services.GetRequiredService<Mock<IProjectRepository>>();
        var testRunRepo = _factory.Services.GetRequiredService<Mock<ITestRunRepository>>();
        var jobSender = _factory.Services.GetRequiredService<Mock<ITestRunJobSender>>();

        projectRepo.Setup(r => r.GetByProjectIdAsync("proj1", It.IsAny<CancellationToken>())).ReturnsAsync(MakeProject());
        testRunRepo.Setup(r => r.GetActiveRunAsync("proj1", It.IsAny<CancellationToken>())).ReturnsAsync((TestRun?)null);
        testRunRepo.Setup(r => r.CreateAsync(It.IsAny<TestRun>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TestRun r, CancellationToken _) => r);
        jobSender.Setup(s => s.SendAsync(It.IsAny<TestRunJobMessage>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var client = CreateClient();
        var response = await PostWebhookAsync(client, MakePayload());

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    [Fact]
    public async Task PostWebhook_WrongIssueType_Returns200AndDoesNotEnqueue()
    {
        GetProjectRepoMock()
            .Setup(r => r.GetByProjectIdAsync("proj1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeProject());

        var client = CreateClient();
        var response = await PostWebhookAsync(client, MakePayload(issueType: "Bug"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task PostWebhook_MissingDescription_Returns200AndSkips()
    {
        var projectRepo = _factory.Services.GetRequiredService<Mock<IProjectRepository>>();
        var testRunRepo = _factory.Services.GetRequiredService<Mock<ITestRunRepository>>();
        var jiraClient = _factory.Services.GetRequiredService<Mock<IJiraApiClient>>();

        projectRepo.Setup(r => r.GetByProjectIdAsync("proj1", It.IsAny<CancellationToken>())).ReturnsAsync(MakeProject());
        testRunRepo.Setup(r => r.CreateAsync(It.IsAny<TestRun>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TestRun r, CancellationToken _) => r);
        jiraClient.Setup(c => c.PostCommentAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var client = CreateClient();
        var response = await PostWebhookAsync(client, MakePayload(description: null));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        testRunRepo.Verify(r => r.CreateAsync(
            It.Is<TestRun>(t => t.Status == TestRunStatus.Skipped),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    public class ApiFactory : WebApplicationFactory<Program>
    {
        private readonly Mock<IProjectRepository> _projectRepo = new();
        private readonly Mock<ITestRunRepository> _testRunRepo = new();
        private readonly Mock<IRunQueueRepository> _runQueueRepo = new();
        private readonly Mock<ITestRunJobSender> _jobSender = new();
        private readonly Mock<IJiraApiClient> _jiraApiClient = new();

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
                    ["Infrastructure:TestRunJobQueueName"] = "test-runs"
                });
            });

            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton(_projectRepo);
                services.AddSingleton(_testRunRepo);
                services.AddSingleton(_runQueueRepo);
                services.AddSingleton(_jobSender);
                services.AddSingleton(_jiraApiClient);

                services.Replace(ServiceDescriptor.Singleton<IProjectRepository>(_ => _projectRepo.Object));
                services.Replace(ServiceDescriptor.Singleton<ITestRunRepository>(_ => _testRunRepo.Object));
                services.Replace(ServiceDescriptor.Singleton<IRunQueueRepository>(_ => _runQueueRepo.Object));
                services.Replace(ServiceDescriptor.Singleton<ITestRunJobSender>(_ => _jobSender.Object));
                services.Replace(ServiceDescriptor.Singleton<IJiraApiClient>(_ => _jiraApiClient.Object));
                services.Replace(ServiceDescriptor.Singleton<ISecretResolver>(_ => new PassthroughSecretResolver()));
            });
        }
    }
}
