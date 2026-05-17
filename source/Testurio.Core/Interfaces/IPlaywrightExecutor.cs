using Testurio.Core.Entities;
using Testurio.Core.Models;

namespace Testurio.Core.Interfaces;

/// <summary>
/// Contract for the Playwright executor component of stage 5 (feature 0029).
/// Drives a headless Chromium browser through each <see cref="UiE2eTestScenario"/>'s steps,
/// captures screenshots on assertion-step failures, and returns typed results.
/// </summary>
public interface IPlaywrightExecutor
{
    /// <summary>
    /// Executes all <paramref name="scenarios"/> sequentially in a single headless Chromium
    /// browser instance (one isolated context per scenario) and returns the typed results.
    /// </summary>
    /// <param name="scenarios">
    /// UI end-to-end test scenarios to execute. Must be non-empty; callers (typically
    /// <see cref="IExecutorRouter"/>) are responsible for not invoking this method
    /// with an empty list.
    /// </param>
    /// <param name="projectConfig">
    /// Project configuration providing <c>ProductUrl</c> and environment access credentials.
    /// </param>
    /// <param name="userId">
    /// Owning user identifier used to construct the Blob Storage path for screenshots:
    /// <c>{userId}/{runId}/{scenarioId}/step-{stepIndex}.png</c>.
    /// </param>
    /// <param name="runId">
    /// Current test run identifier used in the Blob Storage path for screenshots.
    /// </param>
    /// <param name="ct">Cancellation token forwarded to all Playwright and Blob operations.</param>
    /// <returns>
    /// One <see cref="UiE2eScenarioResult"/> per input scenario, in the same order.
    /// Never <c>null</c>; each entry is always present even when the scenario failed.
    /// </returns>
    Task<IReadOnlyList<UiE2eScenarioResult>> ExecuteAsync(
        IReadOnlyList<UiE2eTestScenario> scenarios,
        Project projectConfig,
        Guid userId,
        Guid runId,
        CancellationToken ct = default);
}
