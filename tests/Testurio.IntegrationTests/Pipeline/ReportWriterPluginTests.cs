using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Testurio.Core.Entities;
using Testurio.Core.Enums;
using Testurio.Core.Interfaces;
using Testurio.Core.Models;
using Testurio.Core.Repositories;
using Testurio.Plugins.ReportWriterPlugin;

namespace Testurio.IntegrationTests.Pipeline;

/// <summary>
/// Integration-level tests for <see cref="ReportWriterPlugin"/> that verify
/// template loading, custom template rendering, attachment toggle effects,
/// and fallback behaviour (AC-028–AC-034).
/// All I/O dependencies are mocked; rendering logic is exercised end-to-end
/// through the real <see cref="ReportBuilderService"/> and <see cref="TemplateRenderer"/>.
/// </summary>
public class ReportWriterPluginTests
{
    private readonly Mock<ITestRunRepository> _testRunRepo = new();
    private readonly Mock<ITestScenarioRepository> _scenarioRepo = new();
    private readonly Mock<IStepResultRepository> _stepResultRepo = new();
    private readonly Mock<IExecutionLogRepository> _executionLogRepo = new();
    private readonly Mock<IProjectRepository> _projectRepo = new();
    private readonly Mock<IJiraApiClient> _jiraApiClient = new();
    private readonly Mock<ISecretResolver> _secretResolver = new();
    private readonly Mock<ITemplateRepository> _templateRepo = new();
    private readonly Mock<IBlobStorageClient> _blobStorageClient = new();
    private readonly ReportBuilderService _reportBuilder = new();

    public ReportWriterPluginTests()
    {
        // Default: no log entries.
        _executionLogRepo
            .Setup(r => r.GetByRunAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ExecutionLogEntry>());

        // Default: no custom template.
        _templateRepo
            .Setup(r => r.DownloadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        // Default: blob upload returns a valid URI.
        _blobStorageClient
            .Setup(c => c.UploadAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://storage.example.com/reports/run1/report.md");

        // Default: test run update succeeds.
        _testRunRepo
            .Setup(r => r.UpdateAsync(It.IsAny<TestRun>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TestRun r, CancellationToken _) => r);

        // Default: secret resolver returns a token.
        _secretResolver
            .Setup(r => r.ResolveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("api-token");

        // Default: Jira comment succeeds.
        _jiraApiClient
            .Setup(c => c.PostCommentAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(JiraCommentResult.Success());
    }

    private ReportWriterPlugin CreateSut() => new(
        _testRunRepo.Object,
        _scenarioRepo.Object,
        _stepResultRepo.Object,
        _executionLogRepo.Object,
        _projectRepo.Object,
        _jiraApiClient.Object,
        _secretResolver.Object,
        _reportBuilder,
        _templateRepo.Object,
        _blobStorageClient.Object,
        NullLogger<ReportWriterPlugin>.Instance);

    private static TestRun MakeRun(TestRunStatus status = TestRunStatus.Completed) => new()
    {
        Id = "run1",
        ProjectId = "proj1",
        UserId = "user1",
        JiraIssueKey = "PROJ-1",
        JiraIssueId = "10001",
        Status = status,
        CreatedAt = DateTimeOffset.UtcNow,
        CompletedAt = DateTimeOffset.UtcNow,
    };

    private static Project MakeProject(
        string? templateUri = null,
        bool includeLogs = false,
        bool includeScreenshots = false) => new()
    {
        Id = "proj1",
        UserId = "user1",
        Name = "Test Project",
        ProductUrl = "https://app.example.com",
        TestingStrategy = "API tests",
        JiraBaseUrl = "https://example.atlassian.net",
        JiraProjectKey = "PROJ",
        JiraEmail = "qa@example.com",
        JiraApiTokenSecretRef = "secret-ref",
        JiraWebhookSecretRef = "webhook-secret",
        InTestingStatusLabel = "In Testing",
        ReportTemplateUri = templateUri,
        ReportIncludeLogs = includeLogs,
        ReportIncludeScreenshots = includeScreenshots,
    };

    private static TestScenario MakeScenario(string id = "s1", string title = "Login flow") => new()
    {
        Id = id,
        TestRunId = "run1",
        ProjectId = "proj1",
        UserId = "user1",
        Title = title,
        Steps = new List<TestScenarioStep>(),
    };

    private static StepResult MakeStep(
        string scenarioId = "s1",
        StepStatus status = StepStatus.Passed,
        int durationMs = 150) => new()
    {
        TestRunId = "run1",
        ScenarioId = scenarioId,
        ProjectId = "proj1",
        UserId = "user1",
        StepTitle = "POST /api/login",
        Status = status,
        ExpectedStatusCode = 200,
        ActualStatusCode = 200,
        DurationMs = durationMs,
    };

    // ─── AC-028: custom template is used when project.ReportTemplateUri is set ─

    [Fact]
    public async Task DeliverAsync_UsesCustomTemplate_WhenProjectHasTemplateUri()
    {
        const string customTemplate = "# Custom Report\n\nResult: {{overall_result}}\n\nScenarios:\n{{scenarios}}";

        var run = MakeRun();
        var project = MakeProject(templateUri: "https://blob.example.com/templates/proj1/template.md");

        _testRunRepo.Setup(r => r.GetByIdAsync("proj1", "run1", It.IsAny<CancellationToken>())).ReturnsAsync(run);
        _projectRepo.Setup(r => r.GetByProjectIdAsync("proj1", It.IsAny<CancellationToken>())).ReturnsAsync(project);
        _scenarioRepo.Setup(r => r.GetByRunAsync("proj1", "run1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { MakeScenario() });
        _stepResultRepo.Setup(r => r.GetByRunAsync("proj1", "run1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { MakeStep() });

        _templateRepo
            .Setup(r => r.DownloadAsync("https://blob.example.com/templates/proj1/template.md", It.IsAny<CancellationToken>()))
            .ReturnsAsync(customTemplate);

        string? capturedContent = null;
        _blobStorageClient
            .Setup(c => c.UploadAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>((_, content, _) => capturedContent = content)
            .ReturnsAsync("https://storage.example.com/reports/run1/report.md");

        var sut = CreateSut();
        var result = await sut.DeliverAsync("proj1", "run1");

        Assert.True(result.IsSuccess);
        Assert.NotNull(capturedContent);
        // Custom template starts with "# Custom Report"; default starts with "# Testurio Test Report"
        Assert.StartsWith("# Custom Report", capturedContent);
    }

    // ─── AC-029: default template used when no template URI is set ────────────

    [Fact]
    public async Task DeliverAsync_UsesDefaultTemplate_WhenNoTemplateUriSet()
    {
        var run = MakeRun();
        var project = MakeProject(templateUri: null);

        _testRunRepo.Setup(r => r.GetByIdAsync("proj1", "run1", It.IsAny<CancellationToken>())).ReturnsAsync(run);
        _projectRepo.Setup(r => r.GetByProjectIdAsync("proj1", It.IsAny<CancellationToken>())).ReturnsAsync(project);
        _scenarioRepo.Setup(r => r.GetByRunAsync("proj1", "run1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<TestScenario>());
        _stepResultRepo.Setup(r => r.GetByRunAsync("proj1", "run1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<StepResult>());

        string? capturedContent = null;
        _blobStorageClient
            .Setup(c => c.UploadAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>((_, content, _) => capturedContent = content)
            .ReturnsAsync("https://storage.example.com/reports/run1/report.md");

        var sut = CreateSut();
        var result = await sut.DeliverAsync("proj1", "run1");

        Assert.True(result.IsSuccess);
        Assert.NotNull(capturedContent);
        // Default template contains the default header
        Assert.Contains("Testurio Test Report", capturedContent);

        // Template repo should never have been called since no URI was set
        _templateRepo.Verify(r => r.DownloadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ─── AC-034: fallback to default template when blob fetch fails ──────────

    [Fact]
    public async Task DeliverAsync_FallsBackToDefault_WhenTemplateBlobFetchFails()
    {
        var run = MakeRun();
        var project = MakeProject(templateUri: "https://blob.example.com/templates/proj1/missing.md");

        _testRunRepo.Setup(r => r.GetByIdAsync("proj1", "run1", It.IsAny<CancellationToken>())).ReturnsAsync(run);
        _projectRepo.Setup(r => r.GetByProjectIdAsync("proj1", It.IsAny<CancellationToken>())).ReturnsAsync(project);
        _scenarioRepo.Setup(r => r.GetByRunAsync("proj1", "run1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<TestScenario>());
        _stepResultRepo.Setup(r => r.GetByRunAsync("proj1", "run1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<StepResult>());

        // Blob fetch returns null — simulates missing blob
        _templateRepo
            .Setup(r => r.DownloadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        string? capturedContent = null;
        _blobStorageClient
            .Setup(c => c.UploadAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>((_, content, _) => capturedContent = content)
            .ReturnsAsync("https://storage.example.com/reports/run1/report.md");

        var sut = CreateSut();
        var result = await sut.DeliverAsync("proj1", "run1");

        // Delivery should still succeed despite template fetch failure
        Assert.True(result.IsSuccess);
        Assert.NotNull(capturedContent);
        Assert.Contains("Testurio Test Report", capturedContent);

        // Warning should have been recorded on the run
        _testRunRepo.Verify(r => r.UpdateAsync(
            It.Is<TestRun>(t => t.ReportTemplateWarning != null && t.ReportTemplateWarning.Contains("missing.md")),
            It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    // ─── AC-032: logs included when reportIncludeLogs is true ─────────────────

    [Fact]
    public async Task DeliverAsync_IncludesLogs_WhenReportIncludeLogsIsTrue()
    {
        var run = MakeRun();
        var project = MakeProject(includeLogs: true);

        _testRunRepo.Setup(r => r.GetByIdAsync("proj1", "run1", It.IsAny<CancellationToken>())).ReturnsAsync(run);
        _projectRepo.Setup(r => r.GetByProjectIdAsync("proj1", It.IsAny<CancellationToken>())).ReturnsAsync(project);
        _scenarioRepo.Setup(r => r.GetByRunAsync("proj1", "run1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { MakeScenario() });
        _stepResultRepo.Setup(r => r.GetByRunAsync("proj1", "run1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { MakeStep() });

        var logEntry = new ExecutionLogEntry
        {
            Id = "log1",
            TestRunId = "run1",
            ScenarioId = "s1",
            ProjectId = "proj1",
            UserId = "user1",
            StepIndex = 0,
            StepTitle = "POST /api/login",
            HttpMethod = "POST",
            RequestUrl = "https://app.example.com/api/login",
            RequestHeaders = new Dictionary<string, string>(),
            ResponseHeaders = new Dictionary<string, string>(),
            ResponseStatusCode = 200,
            ResponseBodyInline = "{\"token\":\"abc\"}",
            DurationMs = 50,
        };
        _executionLogRepo
            .Setup(r => r.GetByRunAsync("proj1", "run1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { logEntry });

        string? capturedContent = null;
        _blobStorageClient
            .Setup(c => c.UploadAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>((_, content, _) => capturedContent = content)
            .ReturnsAsync("https://storage.example.com/reports/run1/report.md");

        var sut = CreateSut();
        var result = await sut.DeliverAsync("proj1", "run1");

        Assert.True(result.IsSuccess);
        Assert.NotNull(capturedContent);
        // Execution log section should be present in the rendered output
        Assert.Contains("Execution Logs", capturedContent);
        Assert.Contains("POST /api/login", capturedContent);
    }

    // ─── AC-032: logs excluded when reportIncludeLogs is false ────────────────

    [Fact]
    public async Task DeliverAsync_ExcludesLogs_WhenReportIncludeLogsIsFalse()
    {
        var run = MakeRun();
        var project = MakeProject(includeLogs: false);

        _testRunRepo.Setup(r => r.GetByIdAsync("proj1", "run1", It.IsAny<CancellationToken>())).ReturnsAsync(run);
        _projectRepo.Setup(r => r.GetByProjectIdAsync("proj1", It.IsAny<CancellationToken>())).ReturnsAsync(project);
        _scenarioRepo.Setup(r => r.GetByRunAsync("proj1", "run1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { MakeScenario() });
        _stepResultRepo.Setup(r => r.GetByRunAsync("proj1", "run1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { MakeStep() });

        var logEntry = new ExecutionLogEntry
        {
            Id = "log1",
            TestRunId = "run1",
            ScenarioId = "s1",
            ProjectId = "proj1",
            UserId = "user1",
            StepIndex = 0,
            StepTitle = "POST /api/login",
            HttpMethod = "POST",
            RequestUrl = "https://app.example.com/api/login",
            RequestHeaders = new Dictionary<string, string>(),
            ResponseHeaders = new Dictionary<string, string>(),
            ResponseStatusCode = 200,
            DurationMs = 50,
        };
        _executionLogRepo
            .Setup(r => r.GetByRunAsync("proj1", "run1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { logEntry });

        string? capturedContent = null;
        _blobStorageClient
            .Setup(c => c.UploadAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>((_, content, _) => capturedContent = content)
            .ReturnsAsync("https://storage.example.com/reports/run1/report.md");

        var sut = CreateSut();
        var result = await sut.DeliverAsync("proj1", "run1");

        Assert.True(result.IsSuccess);
        Assert.NotNull(capturedContent);
        // Log section should be empty — request URL must not appear
        Assert.DoesNotContain("POST /api/login", capturedContent);
    }

    // ─── AC-033: rendered report blob URI is stored on the test run ──────────

    [Fact]
    public async Task DeliverAsync_StoresReportBlobUri_OnTestRun()
    {
        const string expectedBlobUri = "https://storage.example.com/reports/proj1/run1/report.md";

        var run = MakeRun();
        var project = MakeProject();

        _testRunRepo.Setup(r => r.GetByIdAsync("proj1", "run1", It.IsAny<CancellationToken>())).ReturnsAsync(run);
        _projectRepo.Setup(r => r.GetByProjectIdAsync("proj1", It.IsAny<CancellationToken>())).ReturnsAsync(project);
        _scenarioRepo.Setup(r => r.GetByRunAsync("proj1", "run1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<TestScenario>());
        _stepResultRepo.Setup(r => r.GetByRunAsync("proj1", "run1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<StepResult>());

        _blobStorageClient
            .Setup(c => c.UploadAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedBlobUri);

        var sut = CreateSut();
        var result = await sut.DeliverAsync("proj1", "run1");

        Assert.True(result.IsSuccess);

        // The run should have been updated with the blob URI
        _testRunRepo.Verify(r => r.UpdateAsync(
            It.Is<TestRun>(t => t.ReportBlobUri == expectedBlobUri),
            It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    // ─── AC-031: template tokens are substituted correctly ───────────────────

    [Fact]
    public async Task DeliverAsync_RendersAllTokens_WhenCustomTemplateUsed()
    {
        const string customTemplate =
            "Result: {{overall_result}}\nScenarios:\n{{scenarios}}\nTiming:\n{{timing_summary}}";

        var run = MakeRun(TestRunStatus.Completed);
        var project = MakeProject(templateUri: "https://blob.example.com/templates/tmpl.md");

        _testRunRepo.Setup(r => r.GetByIdAsync("proj1", "run1", It.IsAny<CancellationToken>())).ReturnsAsync(run);
        _projectRepo.Setup(r => r.GetByProjectIdAsync("proj1", It.IsAny<CancellationToken>())).ReturnsAsync(project);
        _scenarioRepo.Setup(r => r.GetByRunAsync("proj1", "run1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { MakeScenario("s1", "Checkout flow"), MakeScenario("s2", "Login flow") });
        _stepResultRepo.Setup(r => r.GetByRunAsync("proj1", "run1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                MakeStep("s1", StepStatus.Passed, 100),
                MakeStep("s2", StepStatus.Passed, 200),
            });

        _templateRepo
            .Setup(r => r.DownloadAsync("https://blob.example.com/templates/tmpl.md", It.IsAny<CancellationToken>()))
            .ReturnsAsync(customTemplate);

        string? capturedContent = null;
        _blobStorageClient
            .Setup(c => c.UploadAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>((_, content, _) => capturedContent = content)
            .ReturnsAsync("https://storage.example.com/reports/run1/report.md");

        var sut = CreateSut();
        var result = await sut.DeliverAsync("proj1", "run1");

        Assert.True(result.IsSuccess);
        Assert.NotNull(capturedContent);

        Assert.Contains("Result: Passed", capturedContent);
        Assert.Contains("Checkout flow", capturedContent);
        Assert.Contains("Login flow", capturedContent);
        Assert.Contains("Total Duration:", capturedContent);

        // No unsubstituted tokens should remain
        Assert.DoesNotContain("{{overall_result}}", capturedContent);
        Assert.DoesNotContain("{{scenarios}}", capturedContent);
        Assert.DoesNotContain("{{timing_summary}}", capturedContent);
    }

    // ─── AC-033: rendered report is uploaded to blob storage ─────────────────

    [Fact]
    public async Task DeliverAsync_UploadsBlobWithCorrectPath()
    {
        var run = MakeRun();
        var project = MakeProject();

        _testRunRepo.Setup(r => r.GetByIdAsync("proj1", "run1", It.IsAny<CancellationToken>())).ReturnsAsync(run);
        _projectRepo.Setup(r => r.GetByProjectIdAsync("proj1", It.IsAny<CancellationToken>())).ReturnsAsync(project);
        _scenarioRepo.Setup(r => r.GetByRunAsync("proj1", "run1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<TestScenario>());
        _stepResultRepo.Setup(r => r.GetByRunAsync("proj1", "run1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<StepResult>());

        var sut = CreateSut();
        await sut.DeliverAsync("proj1", "run1");

        // Blob path must contain project and run IDs
        _blobStorageClient.Verify(c => c.UploadAsync(
            It.Is<string>(path => path.Contains("proj1") && path.Contains("run1") && path.EndsWith(".md")),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
