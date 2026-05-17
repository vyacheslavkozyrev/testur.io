using Testurio.Core.Entities;
using Testurio.Core.Models;

namespace Testurio.Core.Interfaces;

/// <summary>
/// Contract for the HTTP executor component of stage 5 (feature 0029).
/// Sends one HTTP request per <see cref="ApiTestScenario"/> using the project's
/// <c>ProductUrl</c> as the base URL, evaluates all assertions, and returns
/// typed results — one per scenario, in input order.
/// </summary>
public interface IHttpExecutor
{
    /// <summary>
    /// Executes all <paramref name="scenarios"/> sequentially against the product URL
    /// and returns the typed results.
    /// </summary>
    /// <param name="scenarios">
    /// API test scenarios to execute. Must be non-empty; callers (typically
    /// <see cref="IExecutorRouter"/>) are responsible for not invoking this method
    /// with an empty list.
    /// </param>
    /// <param name="projectConfig">
    /// Project configuration providing <c>ProductUrl</c> and environment access credentials.
    /// </param>
    /// <param name="ct">Cancellation token forwarded to all HTTP calls.</param>
    /// <returns>
    /// One <see cref="ApiScenarioResult"/> per input scenario, in the same order.
    /// Never <c>null</c>; each entry is always present even when the scenario failed.
    /// </returns>
    Task<IReadOnlyList<ApiScenarioResult>> ExecuteAsync(
        IReadOnlyList<ApiTestScenario> scenarios,
        Project projectConfig,
        CancellationToken ct = default);
}
