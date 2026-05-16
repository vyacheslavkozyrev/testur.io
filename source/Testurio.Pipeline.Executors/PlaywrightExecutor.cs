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

    [LoggerMessage(Level = LogLevel.Error,
        Message = "Failed to retrieve access credentials for project {ProjectId}: {ErrorMessage}")]
    private static partial void LogCredentialRetrievalFailed(ILogger logger, string projectId, string errorMessage);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Playwright context options configured for project {ProjectId} (mode: {CredentialType})")]
    private static partial void LogCredentialsApplied(ILogger logger, string projectId, string credentialType);
}
