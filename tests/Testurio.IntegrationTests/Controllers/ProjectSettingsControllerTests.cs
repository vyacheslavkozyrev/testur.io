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
using Testurio.Core.Entities;
using Testurio.Core.Interfaces;
using Testurio.Core.Repositories;
using Testurio.Infrastructure;

namespace Testurio.IntegrationTests.Controllers;

/// <summary>
/// Integration tests for /v1/projects/{projectId}/report-settings endpoints.
/// </summary>
public class ProjectSettingsControllerTests : IClassFixture<ProjectSettingsControllerTests.ApiFactory>
{
    private readonly ApiFactory _factory;

    public ProjectSettingsControllerTests(ApiFactory factory)
    {
        _factory = factory;
        _factory.ResetMocks();
    }

    private static Project MakeProject(
        string userId = "test-user-oid",
        string? templateUri = null,
        bool includeLogs = true,
        bool includeScreenshots = true) => new()
    {
        Id = "proj-001",
        UserId = userId,
        Name = "Test Project",
        ProductUrl = "https://app.example.com",
        TestingStrategy = "API smoke tests.",
        ReportTemplateUri = templateUri,
        ReportIncludeLogs = includeLogs,
        ReportIncludeScreenshots = includeScreenshots,
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow,
    };

    private HttpClient CreateAuthenticatedClient()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "test-token");
        return client;
    }

    // ─── GET /v1/projects/{projectId}/report-settings ────────────────────────

    [Fact]
    public async Task GetReportSettings_Returns200_WithCurrentSettings()
    {
        var project = MakeProject(templateUri: "https://storage.example.com/template.md");
        _factory.ProjectRepoMock
            .Setup(r => r.GetByIdAsync("test-user-oid", "proj-001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(project);

        var client = CreateAuthenticatedClient();
        var response = await client.GetAsync("/v1/projects/proj-001/report-settings");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<ReportSettingsDto>();
        Assert.NotNull(dto);
        Assert.Equal("https://storage.example.com/template.md", dto.ReportTemplateUri);
        Assert.Equal("template.md", dto.ReportTemplateFileName);
        Assert.True(dto.ReportIncludeLogs);
        Assert.True(dto.ReportIncludeScreenshots);
    }

    [Fact]
    public async Task GetReportSettings_Returns404_WhenProjectNotFound()
    {
        _factory.ProjectRepoMock
            .Setup(r => r.GetByIdAsync(It.IsAny<string>(), "proj-001", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Project?)null);

        var client = CreateAuthenticatedClient();
        var response = await client.GetAsync("/v1/projects/proj-001/report-settings");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ─── DELETE /v1/projects/{projectId}/report-settings/template ────────────

    [Fact]
    public async Task RemoveTemplate_Returns204_OnSuccess()
    {
        var project = MakeProject(templateUri: "https://storage.example.com/template.md");
        _factory.ProjectRepoMock
            .Setup(r => r.GetByIdAsync("test-user-oid", "proj-001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(project);
        _factory.TemplateRepoMock
            .Setup(r => r.DeleteAsync("https://storage.example.com/template.md", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _factory.ProjectRepoMock
            .Setup(r => r.UpdateAsync(It.IsAny<Project>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Project p, CancellationToken _) => p);

        var client = CreateAuthenticatedClient();
        var response = await client.DeleteAsync("/v1/projects/proj-001/report-settings/template");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task RemoveTemplate_Returns400_WhenBlobDeletionFails()
    {
        var project = MakeProject(templateUri: "https://storage.example.com/template.md");
        _factory.ProjectRepoMock
            .Setup(r => r.GetByIdAsync("test-user-oid", "proj-001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(project);
        _factory.TemplateRepoMock
            .Setup(r => r.DeleteAsync("https://storage.example.com/template.md", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var client = CreateAuthenticatedClient();
        var response = await client.DeleteAsync("/v1/projects/proj-001/report-settings/template");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ─── POST /v1/projects/{projectId}/report-settings/template ─────────────

    [Fact]
    public async Task UploadTemplate_Returns200_WithBlobUri_ForValidMdFile()
    {
        var project = MakeProject();
        const string blobUri = "https://storage.example.com/templates/proj-001/123-report.md";

        _factory.ProjectRepoMock
            .Setup(r => r.GetByIdAsync("test-user-oid", "proj-001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(project);
        _factory.ProjectRepoMock
            .Setup(r => r.UpdateAsync(It.IsAny<Project>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Project p, CancellationToken _) => p);
        _factory.TemplateRepoMock
            .Setup(r => r.UploadAsync("proj-001", It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(blobUri);

        var client = CreateAuthenticatedClient();
        var content = new MultipartFormDataContent();
        var fileBytes = System.Text.Encoding.UTF8.GetBytes("# Report\n{{story_title}}");
        content.Add(new ByteArrayContent(fileBytes) { Headers = { ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/markdown") } }, "file", "report.md");

        var response = await client.PostAsync("/v1/projects/proj-001/report-settings/template", content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<ReportTemplateUploadResponse>();
        Assert.NotNull(dto);
        Assert.Equal(blobUri, dto.BlobUri);
    }

    [Fact]
    public async Task UploadTemplate_Returns400_ForNonMdFile()
    {
        var project = MakeProject();
        _factory.ProjectRepoMock
            .Setup(r => r.GetByIdAsync("test-user-oid", "proj-001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(project);

        var client = CreateAuthenticatedClient();
        var content = new MultipartFormDataContent();
        var fileBytes = System.Text.Encoding.UTF8.GetBytes("<html><body>not markdown</body></html>");
        content.Add(new ByteArrayContent(fileBytes) { Headers = { ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/html") } }, "file", "report.html");

        var response = await client.PostAsync("/v1/projects/proj-001/report-settings/template", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UploadTemplate_Returns400_ForFileLargerThan100Kb()
    {
        var project = MakeProject();
        _factory.ProjectRepoMock
            .Setup(r => r.GetByIdAsync("test-user-oid", "proj-001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(project);

        var client = CreateAuthenticatedClient();
        var content = new MultipartFormDataContent();
        // 101 KB of ASCII text
        var fileBytes = new byte[101 * 1024];
        Array.Fill(fileBytes, (byte)'a');
        content.Add(new ByteArrayContent(fileBytes) { Headers = { ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/markdown") } }, "file", "large.md");

        var response = await client.PostAsync("/v1/projects/proj-001/report-settings/template", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ─── PATCH /v1/projects/{projectId}/report-settings ─────────────────────

    [Fact]
    public async Task UpdateReportSettings_Returns200_WithUpdatedValues()
    {
        var project = MakeProject();
        _factory.ProjectRepoMock
            .Setup(r => r.GetByIdAsync("test-user-oid", "proj-001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(project);
        _factory.ProjectRepoMock
            .Setup(r => r.UpdateAsync(It.IsAny<Project>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Project p, CancellationToken _) => p);

        var client = CreateAuthenticatedClient();
        var request = new UpdateReportSettingsRequest(false, false);
        var response = await client.PatchAsJsonAsync("/v1/projects/proj-001/report-settings", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<ReportSettingsDto>();
        Assert.NotNull(dto);
        Assert.False(dto.ReportIncludeLogs);
        Assert.False(dto.ReportIncludeScreenshots);
    }

    [Fact]
    public async Task UpdateReportSettings_Returns400_WhenScreenshotsTrueForApiOnlyProject()
    {
        // AC-026: reportIncludeScreenshots must not be true when test_type is api.
        var project = MakeProject();
        project.TestingStrategy = "api";
        _factory.ProjectRepoMock
            .Setup(r => r.GetByIdAsync("test-user-oid", "proj-001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(project);

        var client = CreateAuthenticatedClient();
        var request = new UpdateReportSettingsRequest(true, true);
        var response = await client.PatchAsJsonAsync("/v1/projects/proj-001/report-settings", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ─── Auth guard ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GetReportSettings_Returns401_WithoutAuthToken()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/v1/projects/proj-001/report-settings");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    public class ApiFactory : WebApplicationFactory<Program>
    {
        private readonly Mock<IProjectRepository> _projectRepo = new();
        private readonly Mock<ITemplateRepository> _templateRepo = new();

        public Mock<IProjectRepository> ProjectRepoMock => _projectRepo;
        public Mock<ITemplateRepository> TemplateRepoMock => _templateRepo;

        public void ResetMocks()
        {
            _projectRepo.Reset();
            _templateRepo.Reset();
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
                    ["Infrastructure:BlobStorageConnectionString"] = "DefaultEndpointsProtocol=https;AccountName=test;AccountKey=dummykey==;EndpointSuffix=core.windows.net",
                    ["Infrastructure:ExecutionLogsBlobContainerName"] = "execution-logs",
                    ["Infrastructure:ReportTemplatesBlobContainerName"] = "report-templates",
                    ["Infrastructure:ReportsBlobContainerName"] = "test-reports",
                    ["AzureAdB2C:Authority"] = "https://login.microsoftonline.com/test-tenant",
                    ["AzureAdB2C:ClientId"] = "test-client-id",
                });
            });

            builder.ConfigureTestServices(services =>
            {
                services.Replace(ServiceDescriptor.Singleton<IProjectRepository>(_ => _projectRepo.Object));
                services.Replace(ServiceDescriptor.Singleton<ITemplateRepository>(_ => _templateRepo.Object));
                services.Replace(ServiceDescriptor.Singleton<ISecretResolver>(_ => new Infrastructure.PassthroughSecretResolver()));

                services.AddAuthentication("Test")
                    .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions,
                        TestAuthHandler>("Test", _ => { });
            });
        }
    }
}
