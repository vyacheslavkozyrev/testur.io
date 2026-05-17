using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Path;
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
/// Implements <see cref="IHttpExecutor"/> for stage 5 of the pipeline (feature 0029).
/// </summary>
public sealed partial class HttpExecutor : IHttpExecutor
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

    /// <inheritdoc />
    public async Task<IReadOnlyList<ApiScenarioResult>> ExecuteAsync(
        IReadOnlyList<ApiTestScenario> scenarios,
        Project projectConfig,
        CancellationToken ct = default)
    {
        var client = await CreateAuthenticatedClientAsync(projectConfig, ct);
        var results = new List<ApiScenarioResult>(scenarios.Count);

        foreach (var scenario in scenarios)
        {
            var result = await ExecuteScenarioAsync(client, scenario, projectConfig.ProductUrl, ct);
            results.Add(result);
        }

        return results.AsReadOnly();
    }

    private async Task<ApiScenarioResult> ExecuteScenarioAsync(
        HttpClient client,
        ApiTestScenario scenario,
        string productUrl,
        CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        HttpResponseMessage? response = null;
        string? responseBody = null;
        Exception? requestException = null;

        try
        {
            using var request = BuildRequest(scenario, productUrl);
            response = await client.SendAsync(request, ct);
            responseBody = await response.Content.ReadAsStringAsync(ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            requestException = ex;
        }
        finally
        {
            sw.Stop();
        }

        var assertionResults = EvaluateAssertions(scenario, response, responseBody, requestException);

        return new ApiScenarioResult
        {
            ScenarioId = scenario.Id,
            Title = scenario.Title,
            Passed = assertionResults.All(a => a.Passed),
            DurationMs = sw.ElapsedMilliseconds,
            AssertionResults = assertionResults.AsReadOnly()
        };
    }

    private static HttpRequestMessage BuildRequest(ApiTestScenario scenario, string productUrl)
    {
        var baseUrl = productUrl.TrimEnd('/');
        var path = scenario.Path.StartsWith('/') ? scenario.Path : '/' + scenario.Path;
        var uri = new Uri(baseUrl + path);

        var request = new HttpRequestMessage(new HttpMethod(scenario.Method.ToUpperInvariant()), uri);

        if (scenario.Headers is { Count: > 0 })
        {
            foreach (var (name, value) in scenario.Headers)
                request.Headers.TryAddWithoutValidation(name, value);
        }

        if (scenario.Body is not null &&
            scenario.Method.ToUpperInvariant() is "POST" or "PUT" or "PATCH")
        {
            var json = JsonSerializer.Serialize(scenario.Body);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }

        return request;
    }

    private static List<AssertionResult> EvaluateAssertions(
        ApiTestScenario scenario,
        HttpResponseMessage? response,
        string? responseBody,
        Exception? requestException)
    {
        var results = new List<AssertionResult>(scenario.Assertions.Count);

        foreach (var assertion in scenario.Assertions)
        {
            AssertionResult result;

            if (requestException is not null)
            {
                // AC-016: request itself failed — mark all assertions failed with exception message.
                result = new AssertionResult
                {
                    Type = assertion.Type,
                    Passed = false,
                    Expected = GetExpected(assertion),
                    Actual = requestException.Message
                };
            }
            else
            {
                result = EvaluateAssertion(assertion, response!, responseBody ?? string.Empty);
            }

            results.Add(result);
        }

        return results;
    }

    private static AssertionResult EvaluateAssertion(
        Assertion assertion,
        HttpResponseMessage response,
        string responseBody)
    {
        return assertion switch
        {
            StatusCodeAssertion sc => EvaluateStatusCode(sc, response),
            JsonPathAssertion jp   => EvaluateJsonPath(jp, responseBody),
            HeaderAssertion ha     => EvaluateHeader(ha, response),
            _                      => throw new InvalidOperationException($"Unknown assertion type: {assertion.Type}")
        };
    }

    private static AssertionResult EvaluateStatusCode(StatusCodeAssertion assertion, HttpResponseMessage response)
    {
        var actual = ((int)response.StatusCode).ToString();
        return new AssertionResult
        {
            Type = assertion.Type,
            Passed = (int)response.StatusCode == assertion.Expected,
            Expected = assertion.Expected.ToString(),
            Actual = actual
        };
    }

    private static AssertionResult EvaluateJsonPath(JsonPathAssertion assertion, string responseBody)
    {
        string actual;
        bool passed;

        try
        {
            var jsonPath = JsonPath.Parse(assertion.Path);
            var jsonNode = JsonNode.Parse(responseBody);

            if (jsonNode is null)
            {
                actual = "<no match>";
                passed = false;
            }
            else
            {
                var pathResult = jsonPath.Evaluate(jsonNode);
                var matches = pathResult.Matches;

                if (matches is null || matches.Count == 0)
                {
                    actual = "<no match>";
                    passed = false;
                }
                else
                {
                    var matchedNode = matches[0].Value;

                    if (matchedNode is null)
                    {
                        actual = "<no match>";
                        passed = false;
                    }
                    else
                    {
                        actual = matchedNode is JsonValue jv
                            ? jv.ToJsonString().Trim('"')
                            : matchedNode.ToJsonString();

                        // AC-012: "*" means existence check — any non-null value passes.
                        passed = assertion.Expected == "*" || actual == assertion.Expected;
                    }
                }
            }
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or ArgumentException)
        {
            actual = "<no match>";
            passed = false;
        }

        return new AssertionResult
        {
            Type = assertion.Type,
            Passed = passed,
            Expected = assertion.Expected,
            Actual = actual
        };
    }

    private static AssertionResult EvaluateHeader(HeaderAssertion assertion, HttpResponseMessage response)
    {
        string? headerValue = null;

        // Check both response headers and content headers.
        if (response.Headers.TryGetValues(assertion.Name, out var values))
            headerValue = string.Join(", ", values);
        else if (response.Content.Headers.TryGetValues(assertion.Name, out var contentValues))
            headerValue = string.Join(", ", contentValues);

        var actual = headerValue ?? "<absent>";
        var passed = string.Equals(actual, assertion.Expected, StringComparison.OrdinalIgnoreCase);

        return new AssertionResult
        {
            Type = assertion.Type,
            Passed = passed,
            Expected = assertion.Expected,
            Actual = actual
        };
    }

    private static string GetExpected(Assertion assertion) => assertion switch
    {
        StatusCodeAssertion sc => sc.Expected.ToString(),
        JsonPathAssertion jp   => jp.Expected,
        HeaderAssertion ha     => ha.Expected,
        _                      => string.Empty
    };

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

    [LoggerMessage(Level = LogLevel.Error,
        Message = "Failed to retrieve access credentials for project {ProjectId}: {ErrorMessage}")]
    private static partial void LogCredentialRetrievalFailed(ILogger logger, string projectId, string errorMessage);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Access credentials applied to HTTP client for project {ProjectId} (mode: {CredentialType})")]
    private static partial void LogCredentialsApplied(ILogger logger, string projectId, string credentialType);
}
