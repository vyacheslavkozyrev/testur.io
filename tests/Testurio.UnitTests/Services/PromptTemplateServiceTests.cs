using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Testurio.Core.Interfaces;
using Testurio.Core.Models;
using Testurio.Infrastructure.Prompt;

namespace Testurio.UnitTests.Services;

/// <summary>
/// Minimal <see cref="HybridCache"/> implementation used in unit tests.
/// By default it is a pure pass-through (always invokes the factory).
/// Set <see cref="CachedValue"/> to simulate a warm-cache hit on subsequent calls.
/// </summary>
internal sealed class TestHybridCache : HybridCache
{
    private string? _cachedValue;
    private bool _hasCachedValue;
    private int _callCount;

    /// <summary>When set, <see cref="GetOrCreateAsync{TState,T}"/> returns this value instead of invoking the factory.</summary>
    public string? CachedValue
    {
        set
        {
            _cachedValue = value;
            _hasCachedValue = value is not null;
        }
    }

    public override async ValueTask<T> GetOrCreateAsync<TState, T>(
        string key,
        TState state,
        Func<TState, CancellationToken, ValueTask<T>> factory,
        HybridCacheEntryOptions? options = null,
        IEnumerable<string>? tags = null,
        CancellationToken cancellationToken = default)
    {
        _callCount++;
        if (_hasCachedValue && _callCount > 1)
            return (T)(object)_cachedValue!;

        var result = await factory(state, cancellationToken);
        _cachedValue = result as string;
        _hasCachedValue = true;
        return result;
    }

    public override ValueTask RemoveAsync(string key, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;

    public override ValueTask RemoveByTagAsync(string tag, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;

    public override ValueTask SetAsync<T>(string key, T value, HybridCacheEntryOptions? options = null,
        IEnumerable<string>? tags = null, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;
}

/// <summary>
/// A <see cref="HybridCache"/> whose <see cref="RemoveAsync"/> throws on demand.
/// </summary>
internal sealed class ThrowingRemoveHybridCache : HybridCache
{
    public override ValueTask<T> GetOrCreateAsync<TState, T>(
        string key, TState state, Func<TState, CancellationToken, ValueTask<T>> factory,
        HybridCacheEntryOptions? options = null, IEnumerable<string>? tags = null,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public override ValueTask RemoveAsync(string key, CancellationToken cancellationToken = default)
        => throw new Exception("Cache unavailable");

    public override ValueTask RemoveByTagAsync(string tag, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;

    public override ValueTask SetAsync<T>(string key, T value, HybridCacheEntryOptions? options = null,
        IEnumerable<string>? tags = null, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;
}

/// <summary>
/// A <see cref="HybridCache"/> that records calls to <see cref="RemoveAsync"/> for verification.
/// </summary>
internal sealed class SpyRemoveHybridCache : HybridCache
{
    public List<string> RemovedKeys { get; } = [];

    public override ValueTask<T> GetOrCreateAsync<TState, T>(
        string key, TState state, Func<TState, CancellationToken, ValueTask<T>> factory,
        HybridCacheEntryOptions? options = null, IEnumerable<string>? tags = null,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public override ValueTask RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        RemovedKeys.Add(key);
        return ValueTask.CompletedTask;
    }

    public override ValueTask RemoveByTagAsync(string tag, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;

    public override ValueTask SetAsync<T>(string key, T value, HybridCacheEntryOptions? options = null,
        IEnumerable<string>? tags = null, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;
}

public class PromptTemplateServiceTests
{
    private readonly Mock<IPromptTemplateRepository> _repositoryMock = new();

    private static PromptTemplate MakeTemplate(
        string stage = "story_parser",
        bool isActive = true,
        string body = "You are a test assistant.") =>
        new()
        {
            Id = stage,
            Stage = stage,
            TemplateType = stage,
            Version = 1,
            IsActive = isActive,
            Body = body,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

    private PromptTemplateService CreateSut(HybridCache cache) =>
        new(_repositoryMock.Object, cache, NullLogger<PromptTemplateService>.Instance);

    // ─── GetActiveBodyAsync — returns cached value ────────────────────────────

    [Fact]
    public async Task GetActiveBodyAsync_ReturnsCachedValue_OnSecondCall()
    {
        var template = MakeTemplate();
        _repositoryMock
            .Setup(r => r.GetAsync("story_parser", It.IsAny<CancellationToken>()))
            .ReturnsAsync(template);

        var cache = new TestHybridCache();
        var sut = CreateSut(cache);

        var first = await sut.GetActiveBodyAsync("story_parser");
        var second = await sut.GetActiveBodyAsync("story_parser");

        Assert.Equal(template.Body, first);
        Assert.Equal(template.Body, second);
        // Repository should only be called once (on cache miss)
        _repositoryMock.Verify(r => r.GetAsync("story_parser", It.IsAny<CancellationToken>()), Times.Once);
    }

    // ─── GetActiveBodyAsync — propagates InvalidOperationException when missing ─

    [Fact]
    public async Task GetActiveBodyAsync_ThrowsInvalidOperationException_WhenDocumentMissing()
    {
        _repositoryMock
            .Setup(r => r.GetAsync("story_parser", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("PromptTemplate 'story_parser' not found"));

        var sut = CreateSut(new TestHybridCache());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.GetActiveBodyAsync("story_parser"));
    }

    // ─── GetActiveBodyAsync — throws when IsActive is false ──────────────────

    [Fact]
    public async Task GetActiveBodyAsync_ThrowsInvalidOperationException_WhenIsActiveFalse()
    {
        var inactiveTemplate = MakeTemplate(isActive: false);
        _repositoryMock
            .Setup(r => r.GetAsync("story_parser", It.IsAny<CancellationToken>()))
            .ReturnsAsync(inactiveTemplate);

        var sut = CreateSut(new TestHybridCache());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.GetActiveBodyAsync("story_parser"));

        Assert.Contains("IsActive is false", ex.Message);
        Assert.Contains("story_parser", ex.Message);
    }

    // ─── GetActiveBodyAsync — does not cache error state ─────────────────────

    [Fact]
    public async Task GetActiveBodyAsync_DoesNotCacheErrorState_WhenDocumentMissing()
    {
        // First call: document missing → throws
        // Second call: document now present → succeeds
        var template = MakeTemplate();
        var callCount = 0;

        _repositoryMock
            .Setup(r => r.GetAsync("story_parser", It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                if (callCount == 1)
                    throw new InvalidOperationException("not found");
                return template;
            });

        // The TestHybridCache pass-through always calls the factory, so errors are never cached.
        var sut = CreateSut(new TestHybridCache());

        // First call should throw
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.GetActiveBodyAsync("story_parser"));

        // Second call should succeed — the error was not cached
        var result = await sut.GetActiveBodyAsync("story_parser");
        Assert.Equal(template.Body, result);
    }

    // ─── EvictAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task EvictAsync_CallsHybridCacheRemove_ForCorrectKey()
    {
        var spy = new SpyRemoveHybridCache();
        var sut = CreateSut(spy);

        await sut.EvictAsync("story_parser");

        Assert.Single(spy.RemovedKeys);
        Assert.Equal("prompt-template:story_parser", spy.RemovedKeys[0]);
    }

    [Fact]
    public async Task EvictAsync_DoesNotThrow_WhenCacheRemoveFails()
    {
        var sut = CreateSut(new ThrowingRemoveHybridCache());

        // Should not throw — failure is logged as warning only
        await sut.EvictAsync("story_parser");
    }
}
