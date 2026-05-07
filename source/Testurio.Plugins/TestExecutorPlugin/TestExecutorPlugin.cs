using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Testurio.Core.Entities;
using Testurio.Core.Enums;
using Testurio.Core.Models;

namespace Testurio.Plugins.TestExecutorPlugin;

/// <summary>
/// Executes the HTTP requests defined in test scenario steps, validates responses, and records
/// per-step outcomes. All steps are dispatched in parallel and all scenarios are attempted
/// regardless of individual step outcomes (AC-003, AC-004).
/// </summary>
public sealed partial class TestExecutorPlugin
{
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(10);

    private readonly HttpClient _httpClient;
    private readonly ResponseSchemaValidator _validator;
    private readonly ILogger<TestExecutorPlugin> _logger;

    public TestExecutorPlugin(
        HttpClient httpClient,
        ResponseSchemaValidator validator,
        ILogger<TestExecutorPlugin> logger)
    {
        _httpClient = httpClient;
        _validator = validator;
        _logger = logger;
    }

    /// <summary>
    /// Executes all steps for a single scenario in parallel, returning one <see cref="StepResult"/>
    /// per step. Never throws — malformed, timed-out, or failed steps are captured as results.
    /// </summary>
    public async Task<IReadOnlyList<StepResult>> ExecuteScenarioAsync(
        TestScenario scenario,
        string baseUrl,
        string? bearerToken,
        CancellationToken cancellationToken = default)
    {
        var tasks = scenario.Steps.Select(step =>
            ExecuteStepAsync(step, scenario, baseUrl, bearerToken, cancellationToken));

        var results = await Task.WhenAll(tasks);
        return results;
    }

    private async Task<StepResult> ExecuteStepAsync(
        TestScenarioStep step,
        TestScenario scenario,
        string baseUrl,
        string? bearerToken,
        CancellationToken cancellationToken)
    {
        // Parse description into HTTP method and path (AC-002).
        if (!TryParseStepDefinition(step.Description, out var method, out var path))
        {
            // AC-005: malformed step definition — record Error and skip without throwing.
            LogMalformedStep(_logger, step.Description, scenario.Id);
            return new StepResult
            {
                ProjectId = scenario.ProjectId,
                TestRunId = scenario.TestRunId,
                ScenarioId = scenario.Id,
                StepTitle = step.Description,
                Status = StepStatus.Error,
                FailureMessage = "Error — invalid request definition: could not parse HTTP method and path",
                DurationMs = 0
            };
        }

        var url = BuildUrl(baseUrl, path);

        // Parse expected values from the step's expected result string.
        var expectedStatusCode = _validator.ParseExpectedStatusCode(step.ExpectedResult);
        var expectedSchema = _validator.ParseExpectedSchema(step.ExpectedResult);

        var stopwatch = Stopwatch.StartNew();
        try
        {
            using var request = BuildRequest(method, url, bearerToken);
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(RequestTimeout);

            HttpResponseMessage response;
            try
            {
                response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseContentRead, cts.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // The timeout fired — AC-013/AC-014/AC-015.
                stopwatch.Stop();
                LogTimeout(_logger, url, stopwatch.ElapsedMilliseconds, scenario.Id);
                return new StepResult
                {
                    ProjectId = scenario.ProjectId,
                    TestRunId = scenario.TestRunId,
                    ScenarioId = scenario.Id,
                    StepTitle = step.Description,
                    Status = StepStatus.Timeout,
                    ExpectedStatusCode = expectedStatusCode,
                    ExpectedResponseSchema = expectedSchema,
                    FailureMessage = $"Failed — timeout after {stopwatch.ElapsedMilliseconds} ms",
                    DurationMs = stopwatch.ElapsedMilliseconds
                };
            }

            stopwatch.Stop();

            using (response)
            {
                var actualStatusCode = (int)response.StatusCode;
                var actualBody = await response.Content.ReadAsStringAsync(cancellationToken);
                var actualHeaders = response.Headers
                    .ToDictionary(h => h.Key, h => string.Join(", ", h.Value));

                // AC-009: validate status code.
                var statusFailure = _validator.ValidateStatusCode(actualStatusCode, expectedStatusCode);
                // AC-010: validate body schema.
                var schemaFailure = _validator.ValidateSchema(actualBody, expectedSchema);

                // AC-011: pass only when both validations succeed.
                var passed = statusFailure is null && schemaFailure is null;
                var failureMessage = passed
                    ? null
                    : string.Join("; ", new[] { statusFailure, schemaFailure }.Where(f => f is not null));

                LogStepCompleted(_logger, step.Description, passed ? "Passed" : "Failed", scenario.Id);

                // AC-012: capture actual response regardless of outcome.
                return new StepResult
                {
                    ProjectId = scenario.ProjectId,
                    TestRunId = scenario.TestRunId,
                    ScenarioId = scenario.Id,
                    StepTitle = step.Description,
                    Status = passed ? StepStatus.Passed : StepStatus.Failed,
                    ActualStatusCode = actualStatusCode,
                    ActualResponseBody = actualBody,
                    ActualResponseHeaders = actualHeaders,
                    ExpectedStatusCode = expectedStatusCode,
                    ExpectedResponseSchema = expectedSchema,
                    FailureMessage = failureMessage,
                    DurationMs = stopwatch.ElapsedMilliseconds
                };
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Pipeline-level cancellation — re-throw so the step is not silently swallowed.
            throw;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            // AC-003: execution continues regardless — record Error and return.
            LogStepError(_logger, step.Description, scenario.Id, ex);
            return new StepResult
            {
                ProjectId = scenario.ProjectId,
                TestRunId = scenario.TestRunId,
                ScenarioId = scenario.Id,
                StepTitle = step.Description,
                Status = StepStatus.Error,
                ExpectedStatusCode = expectedStatusCode,
                ExpectedResponseSchema = expectedSchema,
                FailureMessage = $"Error — {ex.GetType().Name}: {ex.Message}",
                DurationMs = stopwatch.ElapsedMilliseconds
            };
        }
    }

    /// <summary>
    /// Parses a step description such as "Send POST /api/orders with valid payload"
    /// into the HTTP method and path components.
    /// </summary>
    private static bool TryParseStepDefinition(string description, out string method, out string path)
    {
        var match = StepDefinitionPattern().Match(description);
        if (!match.Success)
        {
            method = string.Empty;
            path = string.Empty;
            return false;
        }

        method = match.Groups["method"].Value.ToUpperInvariant();
        path = match.Groups["path"].Value;
        return true;
    }

    private static string BuildUrl(string baseUrl, string path)
    {
        var trimmedBase = baseUrl.TrimEnd('/');
        var normalizedPath = path.StartsWith('/') ? path : "/" + path;
        return trimmedBase + normalizedPath;
    }

    private static HttpRequestMessage BuildRequest(string method, string url, string? bearerToken)
    {
        var httpMethod = new HttpMethod(method);
        var request = new HttpRequestMessage(httpMethod, url);

        // AC-006: inject Bearer token; AC-007: skip if none configured; AC-008: never logged.
        if (!string.IsNullOrEmpty(bearerToken))
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", bearerToken);

        return request;
    }

    // Matches "Send POST /api/orders with ..." or simply "GET /health" etc.
    [GeneratedRegex(
        @"(?:send\s+)?(?<method>GET|POST|PUT|PATCH|DELETE|HEAD|OPTIONS)\s+(?<path>/[^\s]*)",
        RegexOptions.IgnoreCase)]
    private static partial Regex StepDefinitionPattern();

    [LoggerMessage(Level = LogLevel.Warning, Message = "Malformed step definition '{Description}' in scenario {ScenarioId} — cannot parse HTTP method and path")]
    private static partial void LogMalformedStep(ILogger logger, string description, string scenarioId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Step '{Url}' timed out after {ElapsedMs} ms in scenario {ScenarioId}")]
    private static partial void LogTimeout(ILogger logger, string url, long elapsedMs, string scenarioId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Step '{Description}' in scenario {ScenarioId} completed with status {Status}")]
    private static partial void LogStepCompleted(ILogger logger, string description, string status, string scenarioId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Step '{Description}' in scenario {ScenarioId} threw an unexpected exception")]
    private static partial void LogStepError(ILogger logger, string description, string scenarioId, Exception ex);
}
