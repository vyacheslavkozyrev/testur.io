using Testurio.Core.Entities;
using Testurio.Core.Models;

namespace Testurio.Core.Interfaces;

/// <summary>
/// Contract for the ExecutorRouter pipeline stage (stage 5 / feature 0029).
/// Dispatches <see cref="ApiTestScenario"/> lists to <see cref="IHttpExecutor"/> and
/// <see cref="UiE2eTestScenario"/> lists to <see cref="IPlaywrightExecutor"/>, running
/// both in parallel when both lists are non-empty, and merges the results into a single
/// <see cref="ExecutionResult"/>.
/// </summary>
public interface IExecutorRouter
{
    /// <summary>
    /// Routes the scenarios in <paramref name="results"/> to the appropriate executor(s)
    /// and returns the merged execution outcome.
    /// </summary>
    /// <param name="results">
    /// Generator output from stage 4. When <see cref="GeneratorResults.ApiScenarios"/> is
    /// non-empty, <see cref="IHttpExecutor"/> is invoked. When
    /// <see cref="GeneratorResults.UiE2eScenarios"/> is non-empty,
    /// <see cref="IPlaywrightExecutor"/> is invoked. Both may be non-empty.
    /// </param>
    /// <param name="projectConfig">
    /// Project configuration providing <c>ProductUrl</c> and environment access settings
    /// forwarded to the executor implementations.
    /// </param>
    /// <param name="userId">
    /// Owning user identifier, forwarded to <see cref="IPlaywrightExecutor"/> for screenshot
    /// upload path construction.
    /// </param>
    /// <param name="runId">
    /// Current test run identifier, forwarded to <see cref="IPlaywrightExecutor"/> for
    /// screenshot upload path construction.
    /// </param>
    /// <param name="ct">Cancellation token forwarded to all executor calls.</param>
    /// <returns>
    /// A merged <see cref="ExecutionResult"/> whose <c>ApiResults</c> and <c>UiE2eResults</c>
    /// are populated from whichever executors were invoked. Lists for non-invoked executors
    /// are empty (never <c>null</c>).
    /// </returns>
    /// <exception cref="Exceptions.ExecutorRouterException">
    /// Thrown when both <see cref="GeneratorResults.ApiScenarios"/> and
    /// <see cref="GeneratorResults.UiE2eScenarios"/> are empty — no executor can be selected.
    /// </exception>
    Task<ExecutionResult> ExecuteAsync(
        GeneratorResults results,
        Project projectConfig,
        Guid userId,
        Guid runId,
        CancellationToken ct = default);
}
