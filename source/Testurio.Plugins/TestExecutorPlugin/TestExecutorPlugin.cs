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
    private readonly LogPersistenceService? _logPersistence;
    private readonly ILogger<TestExecutorPlugin> _logger;

    /// <param name="logPersistence">
    /// Optional.  When supplied (production DI), an <see cref="ExecutionLogEntry"/> is emitted
    /// after every step (AC-001 – AC-004).  When null (tests that do not exercise log capture),
    /// <see cref="EmitLogEntryAsync"/> is a no-op so the plugin degrades gracefully rather than
    /// silently — callers that need log capture must explicitly wire the service.
    /// </param>
    public TestExecutorPlugin(
        HttpClient httpClient,
        ResponseSchemaValidator validator,
        ILogger<TestExecutorPlugin> logger,
        LogPersistenceService? logPersistence = null)
    {
        _httpClient = httpClient;
        _validator = validator;
        _logPersistence = logPersistence;
        _logger = logger;
    }

    /// <summary>
    /// Executes all steps for a single scenario in parallel, returning one <see cref="StepResult"/>
    /// per step. Never throws — timed-out or failed steps are captured as results.
    /// When <see cref="LogPersistenceService"/> is available, an <see cref="ExecutionLogEntry"/>
    /// is emitted for every step regardless of outcome (AC-001, AC-002).
    /// </summary>
    public virtual async Task<IReadOnlyList<StepResult>> ExecuteScenarioAsync(
        TestScenario scenario,
        string baseUrl,
        string? bearerToken,
        CancellationToken cancellationToken = default)
    {
        var tasks = scenario.Steps.Select((step, index) =>
            ExecuteStepAsync(step, index, scenario, baseUrl, bearerToken, cancellationToken));

        var results = await Task.WhenAll(tasks);
        return results;
    }

    private async Task<StepResult> ExecuteStepAsync(
        TestScenarioStep step,
        int stepIndex,
        TestScenario scenario,
        string baseUrl,
        string? bearerToken,
        CancellationToken cancellationToken)
    {
        var url = BuildUrl(baseUrl, step.Path);
        var requestHeaders = BuildRequestHeaders(step.RequestBody, bearerToken);

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

                var timeoutResult = new StepResult
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

                await EmitLogEntryAsync(
                    scenario, stepIndex, step, url, requestHeaders,
                    responseStatusCode: null,
                    responseHeaders: new Dictionary<string, string>(),
                    responseBody: null,
                    durationMs: stopwatch.ElapsedMilliseconds,
                    errorDetail: $"Timeout after {stopwatch.ElapsedMilliseconds} ms",
                    cancellationToken);

                return timeoutResult;
            }

            stopwatch.Stop();

            using (response)
            {
                var actualStatusCode = (int)response.StatusCode;
                var actualBody = await response.Content.ReadAsStringAsync(cancellationToken);
                var responseHeaders = CollectHeaders(response.Headers, response.Content.Headers);

                // AC-009: validate status code; AC-010: validate body schema.
                var statusFailure = _validator.ValidateStatusCode(actualStatusCode, step.ExpectedStatusCode);
                var schemaFailure = _validator.ValidateSchema(actualBody, step.ExpectedResponseSchema);

                // AC-011: pass only when both validations succeed.
                var passed = statusFailure is null && schemaFailure is null;
                var errorDescription = passed
                    ? null
                    : string.Join("; ", new[] { statusFailure, schemaFailure }.Where(f => f is not null));

                LogStepCompleted(_logger, step.Title, passed ? "Passed" : "Failed", scenario.Id);

                var stepResult = new StepResult
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

                await EmitLogEntryAsync(
                    scenario, stepIndex, step, url, requestHeaders,
                    responseStatusCode: actualStatusCode,
                    responseHeaders: responseHeaders,
                    responseBody: actualBody,
                    durationMs: stopwatch.ElapsedMilliseconds,
                    errorDetail: errorDescription,
                    cancellationToken);

                return stepResult;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Pipeline-level cancellation (host shutdown or job cancellation).
            // Log emission is intentionally skipped here: the pipeline is aborting and
            // there is no guarantee that the log repository is still reachable.
            // AC-002 covers Timeout and Error outcomes from within a live execution;
            // mid-flight pipeline cancellation is considered an infrastructure event,
            // not a step outcome, and is not expected to produce a log entry.
            throw;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            // AC-003: execution continues regardless — record Error and return.
            LogStepError(_logger, step.Title, scenario.Id, ex);

            var errorDescription = $"Error — {ex.GetType().Name}: {ex.Message}";

            await EmitLogEntryAsync(
                scenario, stepIndex, step, url, requestHeaders,
                responseStatusCode: null,
                responseHeaders: new Dictionary<string, string>(),
                responseBody: null,
                durationMs: stopwatch.ElapsedMilliseconds,
                errorDetail: errorDescription,
                cancellationToken);

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
                ErrorDescription = errorDescription,
                DurationMs = stopwatch.ElapsedMilliseconds
            };
        }
    }

    /// <summary>
    /// Emits an <see cref="ExecutionLogEntry"/> via <see cref="LogPersistenceService"/> when available.
    /// No-ops when log persistence is not registered.
    /// </summary>
    private async Task EmitLogEntryAsync(
        TestScenario scenario,
        int stepIndex,
        TestScenarioStep step,
        string url,
        Dictionary<string, string> requestHeaders,
        int? responseStatusCode,
        Dictionary<string, string> responseHeaders,
        string? responseBody,
        long durationMs,
        string? errorDetail,
        CancellationToken cancellationToken)
    {
        if (_logPersistence is null)
            return;

        var entry = new ExecutionLogEntry
        {
            TestRunId = scenario.TestRunId,
            ProjectId = scenario.ProjectId,
            UserId = scenario.UserId,
            ScenarioId = scenario.Id,
            StepIndex = stepIndex,
            StepTitle = step.Title,
            HttpMethod = step.Method,
            RequestUrl = url,
            RequestHeaders = requestHeaders,
            RequestBody = step.RequestBody,
            ResponseStatusCode = responseStatusCode,
            ResponseHeaders = responseHeaders,
            ResponseBodyInline = responseBody,
            DurationMs = durationMs,
            ErrorDetail = errorDetail
        };

        await _logPersistence.PersistAsync(entry, cancellationToken);
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

    /// <summary>
    /// Assembles the headers that will be sent with the request.
    /// Authorization is included when a bearer token is provided.
    /// Content-Type is included when a request body is present.
    /// </summary>
    private static Dictionary<string, string> BuildRequestHeaders(string? requestBody, string? bearerToken)
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrEmpty(bearerToken))
            headers["Authorization"] = "Bearer [redacted]";

        if (!string.IsNullOrEmpty(requestBody))
            headers["Content-Type"] = "application/json; charset=utf-8";

        return headers;
    }

    private static Dictionary<string, string> CollectHeaders(
        System.Net.Http.Headers.HttpResponseHeaders responseHeaders,
        System.Net.Http.Headers.HttpContentHeaders contentHeaders)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var header in responseHeaders)
            result[header.Key] = string.Join(", ", header.Value);

        foreach (var header in contentHeaders)
            result[header.Key] = string.Join(", ", header.Value);

        return result;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Step '{Url}' timed out after {ElapsedMs} ms in scenario {ScenarioId}")]
    private static partial void LogTimeout(ILogger logger, string url, long elapsedMs, string scenarioId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Step '{Title}' in scenario {ScenarioId} completed with status {Status}")]
    private static partial void LogStepCompleted(ILogger logger, string title, string status, string scenarioId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Step '{Title}' in scenario {ScenarioId} threw an unexpected exception")]
    private static partial void LogStepError(ILogger logger, string title, string scenarioId, Exception ex);
}
