using Testurio.Core.Entities;
using Testurio.Core.Exceptions;
using Testurio.Core.Interfaces;
using Testurio.Core.Models;

namespace Testurio.Pipeline.Executors;

/// <summary>
/// Routes <see cref="GeneratorResults"/> to <see cref="IHttpExecutor"/> and/or
/// <see cref="IPlaywrightExecutor"/>, running both concurrently when both scenario
/// lists are non-empty, and merges the outcomes into a single <see cref="ExecutionResult"/>.
/// Implements <see cref="IExecutorRouter"/> for stage 5 of the pipeline (feature 0029).
/// </summary>
public sealed class ExecutorRouter : IExecutorRouter
{
    private readonly IHttpExecutor _httpExecutor;
    private readonly IPlaywrightExecutor _playwrightExecutor;

    public ExecutorRouter(IHttpExecutor httpExecutor, IPlaywrightExecutor playwrightExecutor)
    {
        _httpExecutor = httpExecutor;
        _playwrightExecutor = playwrightExecutor;
    }

    /// <inheritdoc />
    public async Task<ExecutionResult> ExecuteAsync(
        GeneratorResults results,
        Project projectConfig,
        Guid userId,
        Guid runId,
        CancellationToken ct = default)
    {
        var hasApi    = results.ApiScenarios.Count > 0;
        var hasUiE2e  = results.UiE2eScenarios.Count > 0;

        // AC-004: both lists empty — cannot select any executor.
        if (!hasApi && !hasUiE2e)
        {
            throw new ExecutorRouterException(
                "No scenarios to execute — both API and UI E2E scenario lists are empty");
        }

        IReadOnlyList<ApiScenarioResult>    apiResults    = [];
        IReadOnlyList<UiE2eScenarioResult>  uiE2eResults  = [];

        if (hasApi && hasUiE2e)
        {
            // AC-001: both lists non-empty → run executors in parallel.
            var apiTask = _httpExecutor.ExecuteAsync(results.ApiScenarios, projectConfig, ct);
            var uiTask  = _playwrightExecutor.ExecuteAsync(results.UiE2eScenarios, projectConfig, userId, runId, ct);

            await Task.WhenAll(apiTask, uiTask);

            apiResults   = await apiTask;
            uiE2eResults = await uiTask;
        }
        else if (hasApi)
        {
            // AC-002: only API list non-empty → only HttpExecutor.
            apiResults = await _httpExecutor.ExecuteAsync(results.ApiScenarios, projectConfig, ct);
        }
        else
        {
            // AC-003: only UI E2E list non-empty → only PlaywrightExecutor.
            uiE2eResults = await _playwrightExecutor.ExecuteAsync(results.UiE2eScenarios, projectConfig, userId, runId, ct);
        }

        return new ExecutionResult
        {
            ApiResults   = apiResults,
            UiE2eResults = uiE2eResults
        };
    }
}
