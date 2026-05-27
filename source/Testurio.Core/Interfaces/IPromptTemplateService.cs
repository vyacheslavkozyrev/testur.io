namespace Testurio.Core.Interfaces;

/// <summary>
/// Caching service that resolves the active prompt body for a pipeline stage from the
/// <c>PromptTemplates</c> Cosmos DB container, with a five-minute TTL cache.
/// <para>
/// Defined in <c>Testurio.Core</c> so that pipeline stage projects can inject the abstraction
/// without a direct dependency on <c>Testurio.Infrastructure</c>.
/// The concrete implementation (<c>PromptTemplateService</c>) lives in <c>Testurio.Infrastructure</c>.
/// </para>
/// </summary>
public interface IPromptTemplateService
{
    /// <summary>
    /// Returns the active prompt <c>Body</c> string for the given pipeline <paramref name="stage"/>.
    /// Results are cached for five minutes under key <c>"prompt-template:{stage}"</c>.
    /// </summary>
    /// <param name="stage">
    /// The pipeline stage key, e.g. <c>"story_parser"</c> or <c>"api_test_generator"</c>.
    /// </param>
    /// <param name="cancellationToken">Propagates notification that the operation should be cancelled.</param>
    /// <returns>The full prompt body string for the stage.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no document exists for the given <paramref name="stage"/> (propagated from the repository),
    /// or when the document's <c>IsActive</c> flag is <c>false</c>.
    /// The exception is never swallowed — it propagates to the pipeline stage caller.
    /// Missing or inactive results are never cached.
    /// </exception>
    Task<string> GetActiveBodyAsync(string stage, CancellationToken cancellationToken = default);

    /// <summary>
    /// Explicitly evicts the cache entry for the given <paramref name="stage"/>.
    /// Called by the admin PUT endpoint after a successful template update so the updated
    /// body is available on the next pipeline run without waiting for the TTL.
    /// Logs a warning if eviction fails but does not throw.
    /// </summary>
    /// <param name="stage">The pipeline stage key whose cache entry should be removed.</param>
    /// <param name="cancellationToken">Propagates notification that the operation should be cancelled.</param>
    Task EvictAsync(string stage, CancellationToken cancellationToken = default);
}
