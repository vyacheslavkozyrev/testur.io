using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using Testurio.Core.Entities;
using Testurio.Core.Exceptions;
using Testurio.Core.Interfaces;
using Testurio.Core.Models;

namespace Testurio.Pipeline.Executors;

/// <summary>
/// Executes UI end-to-end test scenarios using Playwright headless Chromium.
/// Implements <see cref="IPlaywrightExecutor"/> for stage 5 of the pipeline (feature 0029).
/// <para>
/// A single headless Chromium browser instance is launched per invocation and disposed
/// in a <c>finally</c> block after all scenarios complete. Each scenario runs in its own
/// isolated browser context (separate cookies, localStorage, and session state).
/// </para>
/// </summary>
public sealed partial class PlaywrightExecutor : IPlaywrightExecutor
{
    private static readonly string[] AssertionActions = ["assert_visible", "assert_text", "assert_url"];

    private readonly IProjectAccessCredentialProvider _credentialProvider;
    private readonly IScreenshotStorage _screenshotStorage;
    private readonly ILogger<PlaywrightExecutor> _logger;

    public PlaywrightExecutor(
        IProjectAccessCredentialProvider credentialProvider,
        IScreenshotStorage screenshotStorage,
        ILogger<PlaywrightExecutor> logger)
    {
        _credentialProvider = credentialProvider;
        _screenshotStorage = screenshotStorage;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<UiE2eScenarioResult>> ExecuteAsync(
        IReadOnlyList<UiE2eTestScenario> scenarios,
        Project projectConfig,
        Guid userId,
        Guid runId,
        CancellationToken ct = default)
    {
        var contextOptions = await BuildContextOptionsAsync(projectConfig, ct);

        using var playwright = await Playwright.CreateAsync();
        var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true
        });

        try
        {
            var results = new List<UiE2eScenarioResult>(scenarios.Count);

            foreach (var scenario in scenarios)
            {
                var result = await ExecuteScenarioAsync(
                    browser, contextOptions, scenario, userId, runId, ct);
                results.Add(result);
            }

            return results.AsReadOnly();
        }
        finally
        {
            await browser.CloseAsync();
        }
    }

    private async Task<UiE2eScenarioResult> ExecuteScenarioAsync(
        IBrowser browser,
        BrowserNewContextOptions contextOptions,
        UiE2eTestScenario scenario,
        Guid userId,
        Guid runId,
        CancellationToken ct)
    {
        var context = await browser.NewContextAsync(contextOptions);
        var page = await context.NewPageAsync();

        var stepResults = new List<StepExecutionResult>(scenario.Steps.Count);
        var sw = Stopwatch.StartNew();
        bool previousStepFailed = false;

        try
        {
            for (int i = 0; i < scenario.Steps.Count; i++)
            {
                var step = scenario.Steps[i];

                if (previousStepFailed)
                {
                    stepResults.Add(new StepExecutionResult
                    {
                        StepIndex = i,
                        Action = step.Action,
                        Passed = false,
                        ErrorMessage = "Skipped — preceding step failed",
                        ScreenshotBlobUri = null
                    });
                    continue;
                }

                var stepResult = await ExecuteStepAsync(
                    page, step, i, scenario.Id, userId, runId, ct);
                stepResults.Add(stepResult);

                if (!stepResult.Passed)
                    previousStepFailed = true;
            }
        }
        finally
        {
            sw.Stop();
            await context.CloseAsync();
        }

        return new UiE2eScenarioResult
        {
            ScenarioId = scenario.Id,
            Title = scenario.Title,
            Passed = stepResults.All(s => s.Passed),
            DurationMs = sw.ElapsedMilliseconds,
            StepResults = stepResults.AsReadOnly()
        };
    }

    private async Task<StepExecutionResult> ExecuteStepAsync(
        IPage page,
        UiStep step,
        int stepIndex,
        string scenarioId,
        Guid userId,
        Guid runId,
        CancellationToken ct)
    {
        try
        {
            switch (step)
            {
                case NavigateStep navigate:
                    await page.GotoAsync(navigate.Url);
                    break;

                case ClickStep click:
                    await page.ClickAsync(click.Selector);
                    break;

                case FillStep fill:
                    await page.FillAsync(fill.Selector, fill.Value);
                    break;

                case AssertVisibleStep assertVisible:
                    await page.Locator(assertVisible.Selector).WaitForAsync(
                        new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
                    break;

                case AssertTextStep assertText:
                    var element = page.Locator(assertText.Selector);
                    var actualText = (await element.InnerTextAsync()).Trim();
                    if (actualText != assertText.Expected.Trim())
                        throw new PlaywrightAssertionException(
                            $"Expected text '{assertText.Expected.Trim()}' but got '{actualText}'");
                    break;

                case AssertUrlStep assertUrl:
                    var currentUrl = page.Url;
                    bool urlMatches = assertUrl.Expected.EndsWith('*')
                        ? currentUrl.StartsWith(assertUrl.Expected.TrimEnd('*'),
                            StringComparison.Ordinal)
                        : currentUrl == assertUrl.Expected;

                    if (!urlMatches)
                        throw new PlaywrightAssertionException(
                            $"Expected URL '{assertUrl.Expected}' but got '{currentUrl}'");
                    break;

                default:
                    throw new InvalidOperationException($"Unknown step action: {step.Action}");
            }

            return new StepExecutionResult
            {
                StepIndex = stepIndex,
                Action = step.Action,
                Passed = true,
                ErrorMessage = null,
                ScreenshotBlobUri = null
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            string? screenshotBlobUri = null;

            // AC-030: only capture screenshots for failed assertion steps.
            if (AssertionActions.Contains(step.Action))
            {
                screenshotBlobUri = await CaptureScreenshotAsync(
                    page, userId, runId, scenarioId, stepIndex, ct);
            }

            return new StepExecutionResult
            {
                StepIndex = stepIndex,
                Action = step.Action,
                Passed = false,
                ErrorMessage = ex.Message,
                ScreenshotBlobUri = screenshotBlobUri
            };
        }
    }

    /// <summary>
    /// Captures a screenshot and uploads it to Blob Storage.
    /// Returns the blob URI on success, or <c>null</c> if the upload fails.
    /// Upload failure is logged as a structured warning and does not change the step outcome.
    /// </summary>
    private async Task<string?> CaptureScreenshotAsync(
        IPage page,
        Guid userId,
        Guid runId,
        string scenarioId,
        int stepIndex,
        CancellationToken ct)
    {
        try
        {
            var png = await page.ScreenshotAsync(new PageScreenshotOptions
            {
                Type = ScreenshotType.Png
            });

            return await _screenshotStorage.UploadAsync(
                userId.ToString(), runId, scenarioId, stepIndex, png, ct);
        }
        catch (Exception ex)
        {
            LogScreenshotUploadFailed(_logger, runId, scenarioId, stepIndex, ex.Message);
            return null;
        }
    }

    /// <summary>
    /// Resolves access credentials and builds the <see cref="BrowserNewContextOptions"/>
    /// with appropriate authentication configuration for the project's staging environment.
    /// </summary>
    public async Task<BrowserNewContextOptions> BuildContextOptionsAsync(
        Project project, CancellationToken cancellationToken = default)
    {
        ProjectAccessCredentials credentials;
        try
        {
            credentials = await _credentialProvider.ResolveAsync(project, cancellationToken);
        }
        catch (CredentialRetrievalException ex)
        {
            LogCredentialRetrievalFailed(_logger, project.Id, ex.Message);
            throw;
        }

        var options = BuildContextOptions(credentials);
        LogCredentialsApplied(_logger, project.Id, credentials.GetType().Name);
        return options;
    }

    /// <summary>
    /// Builds Playwright browser context options for the given credentials.
    /// Called once per execution run; credential values are not stored after context creation.
    /// </summary>
    internal static BrowserNewContextOptions BuildContextOptions(ProjectAccessCredentials credentials) =>
        credentials switch
        {
            ProjectAccessCredentials.BasicAuth(var username, var password) =>
                new BrowserNewContextOptions
                {
                    HttpCredentials = new HttpCredentials { Username = username, Password = password },
                },

            ProjectAccessCredentials.HeaderToken(var headerName, var headerValue) =>
                new BrowserNewContextOptions
                {
                    ExtraHTTPHeaders = new Dictionary<string, string> { [headerName] = headerValue },
                },

            // IpAllowlist: no credentials needed — worker egress IPs are on the client's allowlist.
            _ => new BrowserNewContextOptions(),
        };

    [LoggerMessage(Level = LogLevel.Error,
        Message = "Failed to retrieve access credentials for project {ProjectId}: {ErrorMessage}")]
    private static partial void LogCredentialRetrievalFailed(ILogger logger, string projectId, string errorMessage);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Playwright context options configured for project {ProjectId} (mode: {CredentialType})")]
    private static partial void LogCredentialsApplied(ILogger logger, string projectId, string credentialType);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Screenshot upload failed for run {RunId}, scenario {ScenarioId}, step {StepIndex}: {ErrorMessage}")]
    private static partial void LogScreenshotUploadFailed(
        ILogger logger, Guid runId, string scenarioId, int stepIndex, string errorMessage);

    /// <summary>
    /// Internal exception used to signal a failed UI assertion within a Playwright step.
    /// Not surfaced outside this class — the message is captured into <see cref="StepExecutionResult.ErrorMessage"/>.
    /// </summary>
    private sealed class PlaywrightAssertionException : Exception
    {
        public PlaywrightAssertionException(string message) : base(message) { }
    }
}
