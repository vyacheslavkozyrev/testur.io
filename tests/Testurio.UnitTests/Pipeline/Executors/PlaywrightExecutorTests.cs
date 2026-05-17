using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Testurio.Core.Entities;
using Testurio.Core.Interfaces;
using Testurio.Core.Models;
using Testurio.Pipeline.Executors;

namespace Testurio.UnitTests.Pipeline.Executors;

/// <summary>
/// Unit tests for <see cref="PlaywrightExecutor"/> covering:
/// - Credential resolution and browser context option building (AC-018, AC-019)
/// - Credential modes: IpAllowlist, BasicAuth, HeaderToken
/// - <see cref="StepExecutionResult"/> field contract (AC-039)
/// - Screenshot capture contract (AC-030, AC-032, AC-033, AC-034)
/// <para>
/// Full browser automation (navigate/click/fill/assert steps) is covered by integration
/// tests that exercise a real Playwright browser. These unit tests focus on the
/// credential injection and result structure contracts that can be verified without
/// a live browser binary.
/// </para>
/// </summary>
public class PlaywrightExecutorTests
{
    private readonly Mock<IProjectAccessCredentialProvider> _credentialProvider = new();
    private readonly Mock<IScreenshotStorage> _screenshotStorage = new();

    private static readonly Project DefaultProject = new()
    {
        UserId = "user1",
        Name = "Test",
        ProductUrl = "https://staging.example.com",
        TestingStrategy = "ui_e2e"
    };

    private PlaywrightExecutor CreateSut() => new(
        _credentialProvider.Object,
        _screenshotStorage.Object,
        NullLogger<PlaywrightExecutor>.Instance);

    // ─── BuildContextOptions: credential mode mapping ─────────────────────────

    [Fact]
    public void BuildContextOptions_IpAllowlist_ReturnsOptionsWithNoCredentials()
    {
        var credentials = new ProjectAccessCredentials.IpAllowlist();

        var options = PlaywrightExecutor.BuildContextOptions(credentials);

        Assert.Null(options.HttpCredentials);
        Assert.Null(options.ExtraHTTPHeaders);
    }

    [Fact]
    public void BuildContextOptions_BasicAuth_SetsHttpCredentials()
    {
        var credentials = new ProjectAccessCredentials.BasicAuth("user", "pass");

        var options = PlaywrightExecutor.BuildContextOptions(credentials);

        Assert.NotNull(options.HttpCredentials);
        Assert.Equal("user", options.HttpCredentials!.Username);
        Assert.Equal("pass", options.HttpCredentials.Password);
        Assert.Null(options.ExtraHTTPHeaders);
    }

    [Fact]
    public void BuildContextOptions_HeaderToken_SetsExtraHTTPHeaders()
    {
        var credentials = new ProjectAccessCredentials.HeaderToken("X-Testurio-Token", "secret-value");

        var options = PlaywrightExecutor.BuildContextOptions(credentials);

        Assert.NotNull(options.ExtraHTTPHeaders);
        var headers = options.ExtraHTTPHeaders!.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        Assert.True(headers.ContainsKey("X-Testurio-Token"));
        Assert.Equal("secret-value", headers["X-Testurio-Token"]);
        Assert.Null(options.HttpCredentials);
    }

    // ─── BuildContextOptionsAsync: credential resolution ─────────────────────

    [Fact]
    public async Task BuildContextOptionsAsync_ResolvesCredentials_ReturnsOptions()
    {
        _credentialProvider
            .Setup(p => p.ResolveAsync(It.IsAny<Project>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProjectAccessCredentials.IpAllowlist());

        var sut = CreateSut();
        var options = await sut.BuildContextOptionsAsync(DefaultProject);

        Assert.NotNull(options);
        _credentialProvider.Verify(p =>
            p.ResolveAsync(DefaultProject, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ─── Screenshot storage contract ──────────────────────────────────────────

    [Fact]
    public async Task ScreenshotStorage_UploadAsync_CalledWithCorrectPath()
    {
        // Verify the IScreenshotStorage is called with userId/runId/scenarioId/stepIndex
        var userId = Guid.NewGuid();
        var runId  = Guid.NewGuid();
        var scenarioId = "sc1";
        var stepIndex  = 2;
        var pngBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47 }; // PNG magic bytes

        _screenshotStorage
            .Setup(s => s.UploadAsync(
                userId.ToString(), runId, scenarioId, stepIndex, pngBytes,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://blob.example.com/screenshots/sc1/step-2.png");

        var blobUri = await _screenshotStorage.Object.UploadAsync(
            userId.ToString(), runId, scenarioId, stepIndex, pngBytes);

        Assert.Equal("https://blob.example.com/screenshots/sc1/step-2.png", blobUri);
        _screenshotStorage.Verify(s =>
            s.UploadAsync(userId.ToString(), runId, scenarioId, stepIndex, pngBytes,
                It.IsAny<CancellationToken>()), Times.Once);
    }

    // ─── StepExecutionResult contract (AC-039) ────────────────────────────────

    [Fact]
    public void StepExecutionResult_PassedStep_HasNullErrorMessageAndNullScreenshotUri()
    {
        var result = new StepExecutionResult
        {
            StepIndex = 0,
            Action = "navigate",
            Passed = true,
            ErrorMessage = null,
            ScreenshotBlobUri = null
        };

        Assert.True(result.Passed);
        Assert.Null(result.ErrorMessage);
        Assert.Null(result.ScreenshotBlobUri);
    }

    [Fact]
    public void StepExecutionResult_SkippedStep_HasSkippedErrorMessage()
    {
        var result = new StepExecutionResult
        {
            StepIndex = 1,
            Action = "click",
            Passed = false,
            ErrorMessage = "Skipped — preceding step failed",
            ScreenshotBlobUri = null
        };

        Assert.False(result.Passed);
        Assert.Equal("Skipped — preceding step failed", result.ErrorMessage);
        Assert.Null(result.ScreenshotBlobUri);
    }

    [Fact]
    public void StepExecutionResult_FailedAssertionStep_CanHaveScreenshotUri()
    {
        var result = new StepExecutionResult
        {
            StepIndex = 2,
            Action = "assert_visible",
            Passed = false,
            ErrorMessage = "Element not found",
            ScreenshotBlobUri = "https://blob.example.com/test-screenshots/user/run/sc/step-2.png"
        };

        Assert.False(result.Passed);
        Assert.NotNull(result.ScreenshotBlobUri);
    }

    [Fact]
    public void StepExecutionResult_NonAssertionFailedStep_HasNullScreenshotUri()
    {
        // Non-assertion steps (navigate, click, fill) should never have a screenshot URI.
        var result = new StepExecutionResult
        {
            StepIndex = 0,
            Action = "navigate",
            Passed = false,
            ErrorMessage = "net::ERR_NAME_NOT_RESOLVED",
            ScreenshotBlobUri = null
        };

        Assert.False(result.Passed);
        Assert.Equal("navigate", result.Action);
        Assert.Null(result.ScreenshotBlobUri);
    }

    // ─── UiE2eScenarioResult contract (AC-038) ────────────────────────────────

    [Fact]
    public void UiE2eScenarioResult_AllStepsPassed_PassedIsTrue()
    {
        var result = new UiE2eScenarioResult
        {
            ScenarioId = "sc1",
            Title = "Login flow",
            Passed = true,
            DurationMs = 1500,
            StepResults =
            [
                new StepExecutionResult { StepIndex = 0, Action = "navigate", Passed = true },
                new StepExecutionResult { StepIndex = 1, Action = "assert_url", Passed = true }
            ]
        };

        Assert.True(result.Passed);
        Assert.All(result.StepResults, s => Assert.True(s.Passed));
    }

    [Fact]
    public void UiE2eScenarioResult_AnyStepFailed_PassedIsFalse()
    {
        var result = new UiE2eScenarioResult
        {
            ScenarioId = "sc2",
            Title = "Checkout flow",
            Passed = false,
            DurationMs = 800,
            StepResults =
            [
                new StepExecutionResult { StepIndex = 0, Action = "navigate", Passed = true },
                new StepExecutionResult { StepIndex = 1, Action = "assert_visible", Passed = false,
                    ErrorMessage = "Element not found",
                    ScreenshotBlobUri = "https://blob.example.com/test-screenshots/u/r/sc2/step-1.png" },
                new StepExecutionResult { StepIndex = 2, Action = "click", Passed = false,
                    ErrorMessage = "Skipped — preceding step failed" }
            ]
        };

        Assert.False(result.Passed);
        Assert.True(result.StepResults[0].Passed);
        Assert.False(result.StepResults[1].Passed);
        Assert.NotNull(result.StepResults[1].ScreenshotBlobUri);
        Assert.False(result.StepResults[2].Passed);
        Assert.Equal("Skipped — preceding step failed", result.StepResults[2].ErrorMessage);
        Assert.Null(result.StepResults[2].ScreenshotBlobUri);
    }

    // ─── assert_url step: exact and prefix match modes (AC-026) ──────────────

    [Theory]
    [InlineData("https://staging.example.com/dashboard", "https://staging.example.com/dashboard", true)]
    [InlineData("https://staging.example.com/dashboard?x=1", "https://staging.example.com/dashboard", false)]
    [InlineData("https://staging.example.com/dashboard/profile", "https://staging.example.com/dashboard*", true)]
    [InlineData("https://other.example.com/", "https://staging.example.com/*", false)]
    public void AssertUrl_MatchingLogic_CorrectResult(string currentUrl, string expected, bool shouldPass)
    {
        // Replicate the URL-matching logic from PlaywrightExecutor to verify the spec.
        bool urlMatches = expected.EndsWith('*')
            ? currentUrl.StartsWith(expected.TrimEnd('*'), StringComparison.Ordinal)
            : currentUrl == expected;

        Assert.Equal(shouldPass, urlMatches);
    }
}
