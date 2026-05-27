using Testurio.Api.DTOs;
using Testurio.Core.Interfaces;
using Testurio.Core.Models;

namespace Testurio.Api.Services;

/// <summary>
/// Admin service for reading and updating prompt template documents.
/// Used by the admin REST endpoints — not called from any pipeline stage.
/// </summary>
public interface IPromptTemplateAdminService
{
    /// <summary>
    /// Returns the <see cref="PromptTemplateDto"/> for the given <paramref name="stage"/>,
    /// or <c>null</c> if no document with that stage key exists.
    /// </summary>
    Task<PromptTemplateDto?> GetAsync(string stage, CancellationToken ct = default);

    /// <summary>
    /// Updates the active document for the given <paramref name="stage"/>: replaces the body,
    /// increments the version, sets <c>IsActive = true</c>, and evicts the cache entry.
    /// </summary>
    /// <returns>The updated <see cref="PromptTemplateDto"/>.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the document does not exist.</exception>
    Task<PromptTemplateDto> UpdateAsync(string stage, string body, CancellationToken ct = default);
}

/// <summary>
/// Concrete implementation of <see cref="IPromptTemplateAdminService"/>.
/// </summary>
public sealed class PromptTemplateAdminService : IPromptTemplateAdminService
{
    private readonly IPromptTemplateRepository _repository;
    private readonly IPromptTemplateService _promptTemplateService;

    public PromptTemplateAdminService(
        IPromptTemplateRepository repository,
        IPromptTemplateService promptTemplateService)
    {
        _repository = repository;
        _promptTemplateService = promptTemplateService;
    }

    /// <inheritdoc />
    public async Task<PromptTemplateDto?> GetAsync(string stage, CancellationToken ct = default)
    {
        var all = await _repository.GetAllAsync(ct);
        var template = all.FirstOrDefault(t =>
            string.Equals(t.Stage, stage, StringComparison.OrdinalIgnoreCase));

        return template is null ? null : MapToDto(template);
    }

    /// <inheritdoc />
    public async Task<PromptTemplateDto> UpdateAsync(string stage, string body, CancellationToken ct = default)
    {
        // Load all documents to find the current one; GetAllAsync does a cross-partition query.
        var all = await _repository.GetAllAsync(ct);
        var existing = all.FirstOrDefault(t =>
            string.Equals(t.Stage, stage, StringComparison.OrdinalIgnoreCase));

        if (existing is null)
            throw new InvalidOperationException(
                $"PromptTemplate '{stage}' not found. Cannot update a document that does not exist.");

        var updated = existing with
        {
            Body = body,
            Version = existing.Version + 1,
            IsActive = true,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        await _repository.UpdateAsync(updated, ct);

        // Evict the cache entry so the next pipeline run gets the updated body immediately.
        // Failure is logged as warning by PromptTemplateService.EvictAsync and does not throw.
        await _promptTemplateService.EvictAsync(stage, ct);

        return MapToDto(updated);
    }

    private static PromptTemplateDto MapToDto(PromptTemplate t) =>
        new(t.Stage, t.Version, t.Body, t.IsActive, t.CreatedAt, t.UpdatedAt);
}
