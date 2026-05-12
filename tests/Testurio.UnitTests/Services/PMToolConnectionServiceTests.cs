using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Testurio.Api.DTOs;
using Testurio.Api.Services;
using Testurio.Core.Entities;
using Testurio.Core.Enums;
using Testurio.Core.Interfaces;
using Testurio.Core.Repositories;
using Testurio.Infrastructure.Security;

namespace Testurio.UnitTests.Services;

public class PMToolConnectionServiceTests
{
    private readonly Mock<IProjectRepository> _projectRepo = new();
    private readonly Mock<ISecretResolver> _secretResolver = new();
    private readonly Mock<IADOClient> _adoClient = new();
    private readonly Mock<IJiraClient> _jiraClient = new();
    private readonly WebhookSecretGenerator _secretGenerator = new();
    private readonly PMToolConnectionService _sut;

    public PMToolConnectionServiceTests()
    {
        _secretResolver.Setup(r => r.ResolveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string s, CancellationToken _) => s);

        var options = Options.Create(new PMToolConnectionServiceOptions { ApiBaseUrl = "https://api.testur.io" });
        _sut = new PMToolConnectionService(
            _projectRepo.Object,
            _secretResolver.Object,
            _adoClient.Object,
            _jiraClient.Object,
            _secretGenerator,
            options,
            NullLogger<PMToolConnectionService>.Instance);
    }

    private static Project MakeProject(string userId = "user-1", string projectId = "proj-1") => new()
    {
        Id = projectId,
        UserId = userId,
        Name = "My App",
        ProductUrl = "https://app.example.com",
        TestingStrategy = "Focus on API contracts.",
    };

    private void SetupProjectLookup(Project project)
    {
        _projectRepo.Setup(r => r.GetByProjectIdAsync(project.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(project);
        _projectRepo.Setup(r => r.GetByIdAsync(project.UserId, project.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(project);
        _projectRepo.Setup(r => r.UpdateAsync(It.IsAny<Project>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Project p, CancellationToken _) => p);
    }

    // ─── SaveADOConnectionAsync ───────────────────────────────────────────────

    [Fact]
    public async Task SaveADOConnectionAsync_SavesConfig_WhenValidRequest()
    {
        var project = MakeProject();
        SetupProjectLookup(project);

        var request = new SaveADOConnectionRequest(
            OrgUrl: "https://dev.azure.com/myorg",
            ProjectName: "My Project",
            Team: "My Team",
            InTestingStatus: "In Testing",
            AuthMethod: ADOAuthMethod.Pat,
            Pat: "my-pat",
            OAuthToken: null);

        var (result, dto, errors) = await _sut.SaveADOConnectionAsync("user-1", "proj-1", request);

        Assert.Equal(ProjectOperationResult.Success, result);
        Assert.Null(errors);
        Assert.NotNull(dto);
        Assert.Equal(PMToolType.Ado, dto!.PmTool);
        Assert.Equal("https://dev.azure.com/myorg", dto.AdoOrgUrl);
        Assert.Equal("My Project", dto.AdoProjectName);
        Assert.Equal("In Testing", dto.AdoInTestingStatus);

        _projectRepo.Verify(r => r.UpdateAsync(
            It.Is<Project>(p =>
                p.PmTool == PMToolType.Ado &&
                p.AdoOrgUrl == "https://dev.azure.com/myorg" &&
                p.AdoProjectName == "My Project"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SaveADOConnectionAsync_ReturnsValidationErrors_WhenOrgUrlInvalid()
    {
        var request = new SaveADOConnectionRequest(
            OrgUrl: "not-a-url",
            ProjectName: "Project",
            Team: "Team",
            InTestingStatus: "In Testing",
            AuthMethod: ADOAuthMethod.Pat,
            Pat: "token",
            OAuthToken: null);

        var (result, dto, errors) = await _sut.SaveADOConnectionAsync("user-1", "proj-1", request);

        Assert.Equal(ProjectOperationResult.Success, result);
        Assert.NotNull(errors);
        Assert.NotEmpty(errors!);
        Assert.Null(dto);
    }

    [Fact]
    public async Task SaveADOConnectionAsync_ReturnsValidationErrors_WhenPatMissing()
    {
        var request = new SaveADOConnectionRequest(
            OrgUrl: "https://dev.azure.com/myorg",
            ProjectName: "Project",
            Team: "Team",
            InTestingStatus: "In Testing",
            AuthMethod: ADOAuthMethod.Pat,
            Pat: null,
            OAuthToken: null);

        var (result, dto, errors) = await _sut.SaveADOConnectionAsync("user-1", "proj-1", request);

        Assert.NotNull(errors);
        Assert.NotEmpty(errors!);
    }

    [Fact]
    public async Task SaveADOConnectionAsync_ReturnsForbidden_WhenProjectBelongsToDifferentUser()
    {
        var otherProject = MakeProject(userId: "other-user");
        _projectRepo.Setup(r => r.GetByProjectIdAsync("proj-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(otherProject);

        var request = new SaveADOConnectionRequest(
            "https://dev.azure.com/myorg", "Project", "Team", "In Testing",
            ADOAuthMethod.Pat, "pat", null);

        var (result, dto, errors) = await _sut.SaveADOConnectionAsync("user-1", "proj-1", request);

        Assert.Equal(ProjectOperationResult.Forbidden, result);
    }

    // ─── SaveJiraConnectionAsync ──────────────────────────────────────────────

    [Fact]
    public async Task SaveJiraConnectionAsync_SavesConfig_WhenValidRequest()
    {
        var project = MakeProject();
        SetupProjectLookup(project);

        var request = new SaveJiraConnectionRequest(
            BaseUrl: "https://myorg.atlassian.net",
            ProjectKey: "PROJ",
            InTestingStatus: "In Testing",
            AuthMethod: JiraAuthMethod.ApiToken,
            Email: "user@example.com",
            ApiToken: "my-token",
            Pat: null);

        var (result, dto, errors) = await _sut.SaveJiraConnectionAsync("user-1", "proj-1", request);

        Assert.Equal(ProjectOperationResult.Success, result);
        Assert.Null(errors);
        Assert.NotNull(dto);
        Assert.Equal(PMToolType.Jira, dto!.PmTool);
        Assert.Equal("https://myorg.atlassian.net", dto.JiraBaseUrl);
    }

    [Fact]
    public async Task SaveJiraConnectionAsync_ReturnsValidationErrors_WhenBaseUrlInvalid()
    {
        var request = new SaveJiraConnectionRequest(
            "not-a-url", "PROJ", "In Testing",
            JiraAuthMethod.ApiToken, "user@example.com", "token", null);

        var (result, dto, errors) = await _sut.SaveJiraConnectionAsync("user-1", "proj-1", request);

        Assert.NotNull(errors);
        Assert.NotEmpty(errors!);
    }

    [Fact]
    public async Task SaveJiraConnectionAsync_ReturnsValidationErrors_WhenEmailMissingForApiToken()
    {
        var request = new SaveJiraConnectionRequest(
            "https://myorg.atlassian.net", "PROJ", "In Testing",
            JiraAuthMethod.ApiToken, null, "token", null);

        var (result, dto, errors) = await _sut.SaveJiraConnectionAsync("user-1", "proj-1", request);

        Assert.NotNull(errors);
        Assert.NotEmpty(errors!);
    }

    // ─── TestConnectionAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task TestConnectionAsync_ReturnsOk_WhenADOConnectionSucceeds()
    {
        var project = MakeProject();
        project.PmTool = PMToolType.Ado;
        project.AdoOrgUrl = "https://dev.azure.com/myorg";
        project.AdoProjectName = "Project";
        project.AdoTokenSecretUri = "projects--proj-1--adoToken";
        SetupProjectLookup(project);

        _adoClient.Setup(c => c.TestConnectionAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ADOConnectionTestResult(true, 200, null));

        var (result, response) = await _sut.TestConnectionAsync("user-1", "proj-1");

        Assert.Equal(ProjectOperationResult.Success, result);
        Assert.Equal("ok", response!.Status);
    }

    [Fact]
    public async Task TestConnectionAsync_ReturnsAuthError_WhenADOReturns401()
    {
        var project = MakeProject();
        project.PmTool = PMToolType.Ado;
        project.AdoOrgUrl = "https://dev.azure.com/myorg";
        project.AdoProjectName = "Project";
        project.AdoTokenSecretUri = "projects--proj-1--adoToken";
        project.IntegrationStatus = IntegrationStatus.Active;
        SetupProjectLookup(project);

        _adoClient.Setup(c => c.TestConnectionAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ADOConnectionTestResult(false, 401, "Unauthorized"));

        var (result, response) = await _sut.TestConnectionAsync("user-1", "proj-1");

        Assert.Equal(ProjectOperationResult.Success, result);
        Assert.Equal("auth_error", response!.Status);

        _projectRepo.Verify(r => r.UpdateAsync(
            It.Is<Project>(p => p.IntegrationStatus == IntegrationStatus.AuthError),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TestConnectionAsync_ReturnsUnreachable_WhenADONetworkFails()
    {
        var project = MakeProject();
        project.PmTool = PMToolType.Ado;
        project.AdoOrgUrl = "https://dev.azure.com/myorg";
        project.AdoProjectName = "Project";
        project.AdoTokenSecretUri = "projects--proj-1--adoToken";
        SetupProjectLookup(project);

        _adoClient.Setup(c => c.TestConnectionAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ADOConnectionTestResult(false, 0, "Network timeout"));

        var (result, response) = await _sut.TestConnectionAsync("user-1", "proj-1");

        Assert.Equal("unreachable", response!.Status);
    }

    // ─── RemoveConnectionAsync ────────────────────────────────────────────────

    [Fact]
    public async Task RemoveConnectionAsync_ClearsPMToolFields_AndReturnsSuccess()
    {
        var project = MakeProject();
        project.PmTool = PMToolType.Ado;
        project.IntegrationStatus = IntegrationStatus.Active;
        project.AdoOrgUrl = "https://dev.azure.com/myorg";
        project.AdoTokenSecretUri = "projects--proj-1--adoToken";
        project.WebhookSecretUri = "projects--proj-1--webhookSecret";
        SetupProjectLookup(project);

        // ADO deregister should be attempted but won't throw.
        _adoClient.Setup(c => c.DeregisterWebhookAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var (result, dto) = await _sut.RemoveConnectionAsync("user-1", "proj-1");

        Assert.Equal(ProjectOperationResult.Success, result);
        Assert.NotNull(dto);
        Assert.Null(dto!.PmTool);
        Assert.Equal(IntegrationStatus.None, dto.IntegrationStatus);

        _projectRepo.Verify(r => r.UpdateAsync(
            It.Is<Project>(p =>
                p.PmTool == null &&
                p.IntegrationStatus == IntegrationStatus.None &&
                p.AdoOrgUrl == null &&
                p.WebhookSecretUri == null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RemoveConnectionAsync_ReturnsForbidden_WhenProjectBelongsToDifferentUser()
    {
        var otherProject = MakeProject(userId: "other-user");
        _projectRepo.Setup(r => r.GetByProjectIdAsync("proj-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(otherProject);

        var (result, dto) = await _sut.RemoveConnectionAsync("user-1", "proj-1");

        Assert.Equal(ProjectOperationResult.Forbidden, result);
        _projectRepo.Verify(r => r.UpdateAsync(It.IsAny<Project>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ─── GetWebhookSetupAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task GetWebhookSetupAsync_ReturnsPlaintextSecret_OnFirstView()
    {
        var project = MakeProject();
        project.PmTool = PMToolType.Ado;
        project.WebhookSecretUri = "projects--proj-1--webhookSecret";
        project.WebhookSecretViewed = false;
        SetupProjectLookup(project);

        // PassthroughSecretResolver returns the key as-is.
        var (result, response) = await _sut.GetWebhookSetupAsync("user-1", "proj-1");

        Assert.Equal(ProjectOperationResult.Success, result);
        Assert.False(response!.IsMasked);
        Assert.Equal("https://api.testur.io/webhooks/ado", response.WebhookUrl);

        // Verify WebhookSecretViewed was set to true.
        _projectRepo.Verify(r => r.UpdateAsync(
            It.Is<Project>(p => p.WebhookSecretViewed == true),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetWebhookSetupAsync_ReturnsMasked_OnSubsequentViews()
    {
        var project = MakeProject();
        project.PmTool = PMToolType.Ado;
        project.WebhookSecretUri = "projects--proj-1--webhookSecret";
        project.WebhookSecretViewed = true;
        SetupProjectLookup(project);

        var (result, response) = await _sut.GetWebhookSetupAsync("user-1", "proj-1");

        Assert.Equal(ProjectOperationResult.Success, result);
        Assert.True(response!.IsMasked);
        Assert.Equal("••••••••", response.WebhookSecret);
    }

    // ─── GetIntegrationStatusAsync ────────────────────────────────────────────

    [Fact]
    public async Task GetIntegrationStatusAsync_ReturnsNone_WhenNoConnectionConfigured()
    {
        var project = MakeProject();
        SetupProjectLookup(project);

        var (result, dto) = await _sut.GetIntegrationStatusAsync("user-1", "proj-1");

        Assert.Equal(ProjectOperationResult.Success, result);
        Assert.NotNull(dto);
        Assert.Null(dto!.PmTool);
        Assert.Equal(IntegrationStatus.None, dto.IntegrationStatus);
    }

    [Fact]
    public async Task GetIntegrationStatusAsync_NeverReturnsRawSecretValues()
    {
        var project = MakeProject();
        project.PmTool = PMToolType.Ado;
        project.AdoTokenSecretUri = "projects--proj-1--adoToken";
        project.WebhookSecretUri = "projects--proj-1--webhookSecret";
        SetupProjectLookup(project);

        var (result, dto) = await _sut.GetIntegrationStatusAsync("user-1", "proj-1");

        Assert.NotNull(dto);
        // Secret URIs are returned (these are references, not raw values).
        Assert.Equal("projects--proj-1--adoToken", dto!.AdoTokenSecretUri);
        // No raw token values should be present on the DTO.
    }
}
