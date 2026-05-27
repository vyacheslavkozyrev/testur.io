using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using Testurio.Core.Interfaces;

namespace Testurio.Infrastructure.Prompt;

/// <summary>
/// Wraps <see cref="IPromptTemplateRepository"/> with <see cref="HybridCache"/> to avoid hitting
/// Cosmos DB on every pipeline run. Results are cached under key
/// <c>"prompt-template:{stage}"</c> for exactly five minutes.
/// <para>
/// Missing or inactive template results are never cached — a subsequent call after the document
/// is created or activated in Cosmos will retrieve the live document.
/// </para>
/// </summary>
public sealed partial class PromptTemplateService : IPromptTemplateService
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    private readonly IPromptTemplateRepository _repository;
    private readonly HybridCache _cache;
    private readonly ILogger<PromptTemplateService> _logger;

    public PromptTemplateService(
        IPromptTemplateRepository repository,
        HybridCache cache,
        ILogger<PromptTemplateService> logger)
    {
        _repository = repository;
        _cache = cache;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<string> GetActiveBodyAsync(string stage, CancellationToken cancellationToken = default)
    {
        // Use GetOrCreateAsync with a factory that throws on missing/inactive templates.
        // When the factory throws, HybridCache does not write a cache entry — so subsequent
        // calls after the document is corrected in Cosmos will re-invoke the factory and
        // retrieve the live document. This satisfies AC-019 (no caching of error state).

        var cacheKey = $"prompt-template:{stage}";

        return await _cache.GetOrCreateAsync(
            cacheKey,
            async ct =>
            {
                var template = await _repository.GetAsync(stage, ct);

                if (!template.IsActive)
                {
                    throw new InvalidOperationException(
                        $"PromptTemplate for stage '{stage}' exists but IsActive is false. " +
                        "Activate a template before starting the worker.");
                }

                return template.Body;
            },
            new HybridCacheEntryOptions { Expiration = CacheTtl },
            cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    public async Task EvictAsync(string stage, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"prompt-template:{stage}";
        try
        {
            await _cache.RemoveAsync(cacheKey, cancellationToken);
        }
        catch (Exception ex)
        {
            LogEvictionFailed(_logger, stage, ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "PromptTemplateService: failed to evict cache entry for stage '{Stage}' — the updated template will be available after the TTL expires")]
    private static partial void LogEvictionFailed(ILogger logger, string stage, Exception ex);
}
