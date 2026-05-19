using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Testurio.Core.Entities;
using Testurio.Core.Enums;
using Testurio.Core.Interfaces;
using Testurio.Infrastructure;
using Xunit;

namespace Testurio.UnitTests.Services;

public class WorkItemTransitionServiceTests
{
    private readonly Mock<IJiraClient> _jiraClient = new();
    private readonly Mock<IADOClient> _adoClient = new();
    private readonly Mock<ISecretResolver> _secretResolver = new();

    private WorkItemTransitionService CreateSut() =>
        new(_jiraClient.Object, _adoClient.Object, _secretResolver.Object,
            NullLogger<WorkItemTransitionService>.Instance);

    private static Project JiraProject(
        string? apiTokenUri = "uri:token",
        string? emailUri = "uri:email",
        JiraAuthMethod authMethod = JiraAuthMethod.ApiToken) =>
        new()
        {
            UserId = "user1",
            Name = "Test",
            ProductUrl = "https://example.com",
            TestingStrategy = "BDD",
            PmTool = PMToolType.Jira,
            JiraBaseUrl = "https://myorg.atlassian.net",
            JiraAuthMethod = authMethod,
            JiraApiTokenSecretUri = apiTokenUri,
            JiraEmailSecretUri = emailUri,
            JiraPatSecretUri = authMethod == JiraAuthMethod.Pat ? "uri:pat" : null,
        };

    private static Project AdoProject(string? tokenUri = "uri:adotoken") =>
        new()
        {
            UserId = "user1",
            Name = "Test",
            ProductUrl = "https://example.com",
            TestingStrategy = "BDD",
            PmTool = PMToolType.Ado,
            AdoOrgUrl = "https://dev.azure.com/my-org",
            AdoProjectName = "MyProject",
            AdoTokenSecretUri = tokenUri,
        };

    private static TestRun JiraTestRun(string issueKey = "PROJ-42", string issueId = "42") =>
        new() { ProjectId = "proj1", UserId = "user1", JiraIssueKey = issueKey, JiraIssueId = issueId };

    private static TestRun AdoTestRun(string workItemId = "99") =>
        new() { ProjectId = "proj1", UserId = "user1", JiraIssueKey = "n/a", JiraIssueId = workItemId };

    // ─── NotConfigured paths ──────────────────────────────────────────────────

    [Fact]
    public async Task TransitionAsync_NullTarget_ReturnsNotConfigured()
    {
        var sut = CreateSut();
        var result = await sut.TransitionAsync(JiraProject(), JiraTestRun(), null);
        Assert.Equal(StatusTransitionOutcome.NotConfigured, result.Outcome);
        Assert.Null(result.TransitionedTo);
    }

    [Fact]
    public async Task TransitionAsync_WhitespaceTarget_ReturnsNotConfigured()
    {
        var sut = CreateSut();
        var result = await sut.TransitionAsync(JiraProject(), JiraTestRun(), "   ");
        Assert.Equal(StatusTransitionOutcome.NotConfigured, result.Outcome);
    }

    [Fact]
    public async Task TransitionAsync_NoPmTool_ReturnsNotConfigured()
    {
        var project = new Project
        {
            UserId = "user1", Name = "T", ProductUrl = "https://x.com", TestingStrategy = "BDD",
            PmTool = null,
        };
        var sut = CreateSut();
        var result = await sut.TransitionAsync(project, JiraTestRun(), "Done");
        Assert.Equal(StatusTransitionOutcome.NotConfigured, result.Outcome);
    }

    // ─── Jira success ─────────────────────────────────────────────────────────

    [Fact]
    public async Task TransitionAsync_Jira_ApiToken_Success_ReturnsSucceeded()
    {
        _secretResolver.Setup(s => s.ResolveAsync("uri:token", It.IsAny<CancellationToken>())).ReturnsAsync("tok");
        _secretResolver.Setup(s => s.ResolveAsync("uri:email", It.IsAny<CancellationToken>())).ReturnsAsync("me@example.com");
        _jiraClient
            .Setup(c => c.TransitionIssueStatusAsync(
                "https://myorg.atlassian.net", "PROJ-42", "me@example.com", "tok", "Done",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new JiraTransitionResult(true, 204, null));

        var sut = CreateSut();
        var result = await sut.TransitionAsync(JiraProject(), JiraTestRun(), "Done");

        Assert.Equal(StatusTransitionOutcome.Succeeded, result.Outcome);
        Assert.Equal("Done", result.TransitionedTo);
        Assert.Null(result.ErrorDetail);
    }

    [Fact]
    public async Task TransitionAsync_Jira_Pat_Success_ReturnsSucceeded()
    {
        var project = JiraProject(authMethod: JiraAuthMethod.Pat);
        _secretResolver.Setup(s => s.ResolveAsync("uri:pat", It.IsAny<CancellationToken>())).ReturnsAsync("myPat");
        _jiraClient
            .Setup(c => c.TransitionIssueStatusAsync(
                "https://myorg.atlassian.net", "PROJ-42", string.Empty, "myPat", "Done",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new JiraTransitionResult(true, 204, null));

        var sut = CreateSut();
        var result = await sut.TransitionAsync(project, JiraTestRun(), "Done");

        Assert.Equal(StatusTransitionOutcome.Succeeded, result.Outcome);
        Assert.Equal("Done", result.TransitionedTo);
    }

    // ─── Jira failure ─────────────────────────────────────────────────────────

    [Fact]
    public async Task TransitionAsync_Jira_ClientReturnsFailure_ReturnsFailed()
    {
        _secretResolver.Setup(s => s.ResolveAsync("uri:token", It.IsAny<CancellationToken>())).ReturnsAsync("tok");
        _secretResolver.Setup(s => s.ResolveAsync("uri:email", It.IsAny<CancellationToken>())).ReturnsAsync("me@example.com");
        _jiraClient
            .Setup(c => c.TransitionIssueStatusAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new JiraTransitionResult(false, 404, "Transition not found"));

        var sut = CreateSut();
        var result = await sut.TransitionAsync(JiraProject(), JiraTestRun(), "Done");

        Assert.Equal(StatusTransitionOutcome.Failed, result.Outcome);
        Assert.Null(result.TransitionedTo);
        Assert.Contains("404", result.ErrorDetail);
    }

    [Fact]
    public async Task TransitionAsync_Jira_MissingBaseUrl_ReturnsFailed()
    {
        var project = JiraProject();
        project.GetType().GetProperty(nameof(Project.JiraBaseUrl))!.SetValue(project, null);
        // Reflection is impractical because the property has a setter — create directly instead:
        var projectNoUrl = new Project
        {
            UserId = "user1", Name = "T", ProductUrl = "https://x.com", TestingStrategy = "BDD",
            PmTool = PMToolType.Jira,
            JiraBaseUrl = null,
            JiraAuthMethod = JiraAuthMethod.ApiToken,
            JiraApiTokenSecretUri = "uri:token",
            JiraEmailSecretUri = "uri:email",
        };

        var sut = CreateSut();
        var result = await sut.TransitionAsync(projectNoUrl, JiraTestRun(), "Done");

        Assert.Equal(StatusTransitionOutcome.Failed, result.Outcome);
        Assert.Contains("missing", result.ErrorDetail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TransitionAsync_Jira_MissingIssueKey_ReturnsFailed()
    {
        _secretResolver.Setup(s => s.ResolveAsync("uri:token", It.IsAny<CancellationToken>())).ReturnsAsync("tok");
        _secretResolver.Setup(s => s.ResolveAsync("uri:email", It.IsAny<CancellationToken>())).ReturnsAsync("me@example.com");

        var sut = CreateSut();
        // JiraIssueKey is required — pass empty string (blank passes the null check but Jira client would fail)
        var run = new TestRun { ProjectId = "p", UserId = "u", JiraIssueKey = "", JiraIssueId = "1" };
        var result = await sut.TransitionAsync(JiraProject(), run, "Done");

        Assert.Equal(StatusTransitionOutcome.Failed, result.Outcome);
    }

    [Fact]
    public async Task TransitionAsync_Jira_MissingApiTokenUri_ReturnsFailed()
    {
        var project = new Project
        {
            UserId = "user1", Name = "T", ProductUrl = "https://x.com", TestingStrategy = "BDD",
            PmTool = PMToolType.Jira,
            JiraBaseUrl = "https://myorg.atlassian.net",
            JiraAuthMethod = JiraAuthMethod.ApiToken,
            JiraApiTokenSecretUri = null,   // missing
            JiraEmailSecretUri = "uri:email",
        };

        var sut = CreateSut();
        var result = await sut.TransitionAsync(project, JiraTestRun(), "Done");

        Assert.Equal(StatusTransitionOutcome.Failed, result.Outcome);
        Assert.Contains("missing", result.ErrorDetail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TransitionAsync_Jira_SecretResolverThrows_ReturnsFailed()
    {
        _secretResolver
            .Setup(s => s.ResolveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Key Vault unavailable"));

        var sut = CreateSut();
        var result = await sut.TransitionAsync(JiraProject(), JiraTestRun(), "Done");

        Assert.Equal(StatusTransitionOutcome.Failed, result.Outcome);
        Assert.Contains("Key Vault", result.ErrorDetail);
    }

    // ─── ADO success ──────────────────────────────────────────────────────────

    [Fact]
    public async Task TransitionAsync_Ado_Success_ReturnsSucceeded()
    {
        _secretResolver.Setup(s => s.ResolveAsync("uri:adotoken", It.IsAny<CancellationToken>())).ReturnsAsync("adoTok");
        _adoClient
            .Setup(c => c.TransitionWorkItemStateAsync(
                "https://dev.azure.com/my-org", 99, "adoTok", "Closed",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ADOTransitionResult(true, 200, null));

        var sut = CreateSut();
        var result = await sut.TransitionAsync(AdoProject(), AdoTestRun("99"), "Closed");

        Assert.Equal(StatusTransitionOutcome.Succeeded, result.Outcome);
        Assert.Equal("Closed", result.TransitionedTo);
        Assert.Null(result.ErrorDetail);
    }

    // ─── ADO failure ──────────────────────────────────────────────────────────

    [Fact]
    public async Task TransitionAsync_Ado_ClientReturnsFailure_ReturnsFailed()
    {
        _secretResolver.Setup(s => s.ResolveAsync("uri:adotoken", It.IsAny<CancellationToken>())).ReturnsAsync("adoTok");
        _adoClient
            .Setup(c => c.TransitionWorkItemStateAsync(
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ADOTransitionResult(false, 400, "Invalid state transition"));

        var sut = CreateSut();
        var result = await sut.TransitionAsync(AdoProject(), AdoTestRun("99"), "Closed");

        Assert.Equal(StatusTransitionOutcome.Failed, result.Outcome);
        Assert.Null(result.TransitionedTo);
        Assert.Contains("400", result.ErrorDetail);
    }

    [Fact]
    public async Task TransitionAsync_Ado_NonIntegerWorkItemId_ReturnsFailed()
    {
        var sut = CreateSut();
        var result = await sut.TransitionAsync(AdoProject(), AdoTestRun("not-an-int"), "Closed");

        Assert.Equal(StatusTransitionOutcome.Failed, result.Outcome);
        Assert.Contains("not a valid integer", result.ErrorDetail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TransitionAsync_Ado_MissingTokenUri_ReturnsFailed()
    {
        var project = AdoProject(tokenUri: null);
        var sut = CreateSut();
        var result = await sut.TransitionAsync(project, AdoTestRun(), "Closed");

        Assert.Equal(StatusTransitionOutcome.Failed, result.Outcome);
        Assert.Contains("missing", result.ErrorDetail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TransitionAsync_Ado_MissingOrgUrl_ReturnsFailed()
    {
        var project = new Project
        {
            UserId = "user1", Name = "T", ProductUrl = "https://x.com", TestingStrategy = "BDD",
            PmTool = PMToolType.Ado,
            AdoOrgUrl = null,
            AdoProjectName = "MyProject",
            AdoTokenSecretUri = "uri:adotoken",
        };

        var sut = CreateSut();
        var result = await sut.TransitionAsync(project, AdoTestRun(), "Closed");

        Assert.Equal(StatusTransitionOutcome.Failed, result.Outcome);
        Assert.Contains("missing", result.ErrorDetail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TransitionAsync_Ado_SecretResolverThrows_ReturnsFailed()
    {
        _secretResolver
            .Setup(s => s.ResolveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Vault error"));

        var sut = CreateSut();
        var result = await sut.TransitionAsync(AdoProject(), AdoTestRun(), "Closed");

        Assert.Equal(StatusTransitionOutcome.Failed, result.Outcome);
        Assert.Contains("Vault error", result.ErrorDetail);
    }
}
