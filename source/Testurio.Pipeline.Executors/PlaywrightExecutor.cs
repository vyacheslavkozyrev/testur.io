using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using Testurio.Core.Entities;
using Testurio.Core.Exceptions;
using Testurio.Core.Interfaces;
using Testurio.Core.Models;

namespace Testurio.Pipeline.Executors;

/// <summary>
/// Executes UI end-to-end test scenarios using Playwright headless Chromium.
/// Resolves environment access credentials via <see cref="IProjectAccessCredentialProvider"/>
/// and applies them to the browser context options (httpCredentials or extraHTTPHeaders)
/// before launching each scenario.
/// Full execution logic is implemented in feature 0029; this file establishes the
/// credential injection pattern for feature 0017.
/// </summary>
public sealed partial class PlaywrightExecutor
{
    private readonly IProjectAccessCredentialProvider _credentialProvider;
    private readonly ILogger<PlaywrightExecutor> _logger;

    public PlaywrightExecutor(
        IProjectAccessCredentialProvider credentialProvider,
        ILogger<PlaywrightExecutor> logger)
    {
        _credentialProvider = credentialProvider;
        _logger = logger;
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

    /// <summary>
    /// Applies the project's configured per-action timeout to a Playwright <see cref="IPage"/>
    /// via <see cref="IPage.SetDefaultTimeout"/>.
    /// </summary>
    /// <remarks>
    /// Call once per browser context (scenario), immediately after page creation.
    /// Because each scenario runs in its own context, <c>SetDefaultTimeout</c> is clean and safe —
    /// it does not affect other concurrent scenarios.
    /// The timeout fires as a <see cref="Microsoft.Playwright.PlaywrightException"/> whose
    /// message contains <c>"Timeout"</c>; callers should catch it and record the step as
    /// <c>Passed: false</c> with <c>ErrorMessage: "Timeout — action exceeded {n}s"</c>.
    /// </remarks>
    /// <param name="page">The Playwright page to configure.</param>
    /// <param name="timeoutSeconds">Per-action timeout from <c>ProjectConfig.RequestTimeoutSeconds</c>.</param>
    public static void ApplyPageTimeout(IPage page, int timeoutSeconds)
    {
        var timeoutMs = (float)(timeoutSeconds * 1000);
        page.SetDefaultTimeout(timeoutMs);
    }

    [LoggerMessage(Level = LogLevel.Error,
        Message = "Failed to retrieve access credentials for project {ProjectId}: {ErrorMessage}")]
    private static partial void LogCredentialRetrievalFailed(ILogger logger, string projectId, string errorMessage);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Playwright context options configured for project {ProjectId} (mode: {CredentialType})")]
    private static partial void LogCredentialsApplied(ILogger logger, string projectId, string credentialType);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Playwright action timed out for project {ProjectId} after {TimeoutSeconds}s (elapsed: {ElapsedMs}ms); remaining steps in scenario skipped")]
    private static partial void LogActionTimedOut(ILogger logger, string projectId, int timeoutSeconds, long elapsedMs);
}
