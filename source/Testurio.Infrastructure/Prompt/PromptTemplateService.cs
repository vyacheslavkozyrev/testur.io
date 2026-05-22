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
        // We do NOT use GetOrCreateAsync here because we must not cache error states.
        // Instead, check the cache manually, then fall through to the repository only on miss.
        // HybridCache does not expose a TryGet API; we use a wrapper approach:
        // attempt to retrieve the body, and if we get a cache hit the factory won't run.
        // On a cache miss the factory runs and either returns the body or throws — if it throws,
        // HybridCache will NOT store the result (the entry is never written on exception).

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
