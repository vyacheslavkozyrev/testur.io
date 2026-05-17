using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Testurio.Core.Entities;
using Testurio.Core.Exceptions;
using Testurio.Core.Interfaces;
using Testurio.Core.Models;

namespace Testurio.Pipeline.Executors;

/// <summary>
/// Executes API test scenarios as HTTP requests against the project's product URL.
/// Resolves environment access credentials via <see cref="IProjectAccessCredentialProvider"/>
/// and applies them to every request before sending.
/// Full execution logic is implemented in feature 0029; this file establishes the
/// credential injection pattern for feature 0017.
/// </summary>
public sealed partial class HttpExecutor
{
    private readonly IProjectAccessCredentialProvider _credentialProvider;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<HttpExecutor> _logger;

    public HttpExecutor(
        IProjectAccessCredentialProvider credentialProvider,
        IHttpClientFactory httpClientFactory,
        ILogger<HttpExecutor> logger)
    {
        _credentialProvider = credentialProvider;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <summary>
    /// Resolves access credentials for the project and returns a pre-configured
    /// <see cref="HttpClient"/> with the appropriate authentication header applied.
    /// </summary>
    public async Task<HttpClient> CreateAuthenticatedClientAsync(
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

        var client = _httpClientFactory.CreateClient("executor");
        ApplyCredentials(client, credentials);
        LogCredentialsApplied(_logger, project.Id, credentials.GetType().Name);
        return client;
    }

    /// <summary>
    /// Applies environment access credentials to an <see cref="HttpClient"/>.
    /// Called once per execution run; credentials are not cached beyond the run.
    /// </summary>
    internal static void ApplyCredentials(HttpClient client, ProjectAccessCredentials credentials)
    {
        switch (credentials)
        {
            case ProjectAccessCredentials.BasicAuth(var username, var password):
                var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));
                client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Basic", encoded);
                break;

            case ProjectAccessCredentials.HeaderToken(var headerName, var headerValue):
                try
                {
                    client.DefaultRequestHeaders.Add(headerName, headerValue);
                }
                catch (Exception ex) when (ex is FormatException or InvalidOperationException)
                {
                    throw new CredentialRetrievalException(
                        $"Header name '{headerName}' is not a valid HTTP header name.", ex);
                }
                break;

            case ProjectAccessCredentials.IpAllowlist:
                // No auth header — worker egress IPs are on the client's allowlist.
                break;
        }
    }

    /// <summary>
    /// Sends a single HTTP request with a per-request timeout derived from
    /// <paramref name="timeoutSeconds"/>, linked to the outer <paramref name="runToken"/>
    /// so cancellation from either source terminates the request.
    /// </summary>
    /// <remarks>
    /// Each call creates an independent <see cref="CancellationTokenSource"/> so that a timeout
    /// on one request does not affect subsequent requests sharing the same <see cref="HttpClient"/>.
    /// </remarks>
    /// <returns>
    /// A tuple of (<see cref="HttpResponseMessage"/>, elapsed milliseconds) on success.
    /// Throws <see cref="OperationCanceledException"/> when <paramref name="runToken"/> is cancelled.
    /// Throws <see cref="TimeoutException"/> when the per-request timeout elapses.
    /// </returns>
    public static async Task<(HttpResponseMessage Response, long ElapsedMs)> SendWithTimeoutAsync(
        HttpClient client,
        HttpRequestMessage request,
        int timeoutSeconds,
        CancellationToken runToken,
        string? projectId = null,
        ILogger? logger = null)
    {
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
        using var linkedCts  = CancellationTokenSource.CreateLinkedTokenSource(runToken, timeoutCts.Token);

        var sw = Stopwatch.StartNew();
        try
        {
            var response = await client.SendAsync(request, linkedCts.Token);
            sw.Stop();
            return (response, sw.ElapsedMilliseconds);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !runToken.IsCancellationRequested)
        {
            sw.Stop();
            if (logger is not null && projectId is not null)
                LogRequestTimedOut(logger, projectId, timeoutSeconds, sw.ElapsedMilliseconds);
            throw new TimeoutException(
                $"Timeout — request exceeded {timeoutSeconds}s",
                new OperationCanceledException(linkedCts.Token));
        }
    }

    [LoggerMessage(Level = LogLevel.Error,
        Message = "Failed to retrieve access credentials for project {ProjectId}: {ErrorMessage}")]
    private static partial void LogCredentialRetrievalFailed(ILogger logger, string projectId, string errorMessage);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Access credentials applied to HTTP client for project {ProjectId} (mode: {CredentialType})")]
    private static partial void LogCredentialsApplied(ILogger logger, string projectId, string credentialType);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "HTTP request timed out for project {ProjectId} after {TimeoutSeconds}s (elapsed: {ElapsedMs}ms)")]
    private static partial void LogRequestTimedOut(ILogger logger, string projectId, int timeoutSeconds, long elapsedMs);
}
