using Testurio.Core.Models;

namespace Testurio.Core.Interfaces;

/// <summary>
/// Repository abstraction for reading and writing prompt template documents in the
/// <c>PromptTemplates</c> Cosmos DB container.
/// Defined in <c>Testurio.Core</c> so that pipeline projects can depend on the abstraction
/// without a direct Azure SDK reference. The concrete implementation lives in
/// <c>Testurio.Infrastructure</c>.
/// <para>
/// Feature 0047: extended with <see cref="UpdateAsync"/> and <see cref="GetAllAsync"/> for
/// admin write operations. Pipeline stages do not call these methods directly — they use
/// <see cref="IPromptTemplateService"/> instead.
/// </para>
/// </summary>
public interface IPromptTemplateRepository
{
    /// <summary>
    /// Retrieves the <see cref="PromptTemplate"/> document for the given <paramref name="stage"/>.
    /// </summary>
    /// <param name="stage">
    /// The stage key, e.g. <c>"story_parser"</c> or <c>"api_test_generator"</c>.
    /// This value is also the Cosmos document <c>id</c>.
    /// </param>
    /// <param name="cancellationToken">Propagates notification that the operation should be cancelled.</param>
    /// <returns>The matching <see cref="PromptTemplate"/>.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no document with the given <paramref name="stage"/> exists in the container.
    /// The caller must fail the pipeline run immediately — no generator agent should be invoked.
    /// </exception>
    Task<PromptTemplate> GetAsync(string stage, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns all <see cref="PromptTemplate"/> documents in the container regardless of
    /// <see cref="PromptTemplate.IsActive"/> status. Used by the admin read endpoint only.
    /// </summary>
    /// <param name="cancellationToken">Propagates notification that the operation should be cancelled.</param>
    /// <returns>All prompt template documents in the container.</returns>
    Task<IReadOnlyList<PromptTemplate>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces the <see cref="PromptTemplate"/> document in Cosmos with the provided <paramref name="template"/>.
    /// Performs a full-document replace keyed by <c>id = template.Stage</c>.
    /// </summary>
    /// <param name="template">The updated template document to write.</param>
    /// <param name="cancellationToken">Propagates notification that the operation should be cancelled.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no document with <c>id = template.Stage</c> exists in the container.
    /// A missing document is treated as a configuration error.
    /// </exception>
    Task UpdateAsync(PromptTemplate template, CancellationToken cancellationToken = default);
}
