using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Testurio.Core.Entities;
using Testurio.Core.Enums;
using Testurio.Core.Models;

namespace Testurio.Plugins.TestExecutorPlugin;

public partial class TestExecutorPlugin
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
    /// per step. Never throws — timed-out or failed steps are captured as results.
    /// </summary>
    public virtual async Task<IReadOnlyList<StepResult>> ExecuteScenarioAsync(
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
        var url = BuildUrl(baseUrl, step.Path);

        var stopwatch = Stopwatch.StartNew();
        try
        {
            using var request = BuildRequest(step.Method, url, step.RequestBody, bearerToken);
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(RequestTimeout);

            HttpResponseMessage response;
            try
            {
                response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseContentRead, cts.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // AC-013/AC-014/AC-015: timeout fired.
                stopwatch.Stop();
                LogTimeout(_logger, url, stopwatch.ElapsedMilliseconds, scenario.Id);
                return new StepResult
                {
                    TestRunId = scenario.TestRunId,
                    ScenarioId = scenario.Id,
                    ProjectId = scenario.ProjectId,
                    UserId = scenario.UserId,
                    StepTitle = step.Title,
                    Status = StepStatus.Timeout,
                    ExpectedStatusCode = step.ExpectedStatusCode,
                    ExpectedResponseSchema = step.ExpectedResponseSchema,
                    ErrorDescription = $"Failed — timeout after {stopwatch.ElapsedMilliseconds} ms",
                    DurationMs = stopwatch.ElapsedMilliseconds
                };
            }

            stopwatch.Stop();

            using (response)
            {
                var actualStatusCode = (int)response.StatusCode;
                var actualBody = await response.Content.ReadAsStringAsync(cancellationToken);

                // AC-009: validate status code; AC-010: validate body schema.
                var statusFailure = _validator.ValidateStatusCode(actualStatusCode, step.ExpectedStatusCode);
                var schemaFailure = _validator.ValidateSchema(actualBody, step.ExpectedResponseSchema);

                // AC-011: pass only when both validations succeed.
                var passed = statusFailure is null && schemaFailure is null;
                var errorDescription = passed
                    ? null
                    : string.Join("; ", new[] { statusFailure, schemaFailure }.Where(f => f is not null));

                LogStepCompleted(_logger, step.Title, passed ? "Passed" : "Failed", scenario.Id);

                // AC-012: capture actual response regardless of outcome.
                return new StepResult
                {
                    TestRunId = scenario.TestRunId,
                    ScenarioId = scenario.Id,
                    ProjectId = scenario.ProjectId,
                    UserId = scenario.UserId,
                    StepTitle = step.Title,
                    Status = passed ? StepStatus.Passed : StepStatus.Failed,
                    ActualStatusCode = actualStatusCode,
                    ActualResponseBody = actualBody,
                    ExpectedStatusCode = step.ExpectedStatusCode,
                    ExpectedResponseSchema = step.ExpectedResponseSchema,
                    ErrorDescription = errorDescription,
                    DurationMs = stopwatch.ElapsedMilliseconds
                };
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            // AC-003: execution continues regardless — record Error and return.
            LogStepError(_logger, step.Title, scenario.Id, ex);
            return new StepResult
            {
                TestRunId = scenario.TestRunId,
                ScenarioId = scenario.Id,
                ProjectId = scenario.ProjectId,
                UserId = scenario.UserId,
                StepTitle = step.Title,
                Status = StepStatus.Error,
                ExpectedStatusCode = step.ExpectedStatusCode,
                ExpectedResponseSchema = step.ExpectedResponseSchema,
                ErrorDescription = $"Error — {ex.GetType().Name}: {ex.Message}",
                DurationMs = stopwatch.ElapsedMilliseconds
            };
        }
    }

    private static string BuildUrl(string baseUrl, string path)
    {
        var trimmedBase = baseUrl.TrimEnd('/');
        var normalizedPath = path.StartsWith('/') ? path : "/" + path;
        return trimmedBase + normalizedPath;
    }

    private static HttpRequestMessage BuildRequest(string method, string url, string? requestBody, string? bearerToken)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), url);

        // AC-006: inject Bearer token; AC-007: skip if none configured; AC-008: never logged.
        if (!string.IsNullOrEmpty(bearerToken))
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", bearerToken);

        if (!string.IsNullOrEmpty(requestBody))
            request.Content = new StringContent(requestBody, System.Text.Encoding.UTF8, "application/json");

        return request;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Step '{Url}' timed out after {ElapsedMs} ms in scenario {ScenarioId}")]
    private static partial void LogTimeout(ILogger logger, string url, long elapsedMs, string scenarioId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Step '{Title}' in scenario {ScenarioId} completed with status {Status}")]
    private static partial void LogStepCompleted(ILogger logger, string title, string status, string scenarioId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Step '{Title}' in scenario {ScenarioId} threw an unexpected exception")]
    private static partial void LogStepError(ILogger logger, string title, string scenarioId, Exception ex);
}
