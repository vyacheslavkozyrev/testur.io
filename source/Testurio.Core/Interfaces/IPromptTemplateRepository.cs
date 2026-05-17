using Testurio.Core.Models;

namespace Testurio.Core.Interfaces;

/// <summary>
/// Repository abstraction for reading prompt template documents from the
/// <c>PromptTemplates</c> Cosmos DB container.
/// Defined in <c>Testurio.Core</c> so that pipeline projects can depend on the abstraction
/// without a direct Azure SDK reference. The concrete implementation lives in
/// <c>Testurio.Infrastructure</c>.
/// </summary>
public interface IPromptTemplateRepository
{
    /// <summary>
    /// Retrieves the <see cref="PromptTemplate"/> document for the given <paramref name="templateType"/>.
    /// </summary>
    /// <param name="templateType">
    /// The template type identifier, e.g. <c>"api_test_generator"</c> or <c>"ui_e2e_test_generator"</c>.
    /// This value is also the Cosmos document <c>id</c>.
    /// </param>
    /// <param name="cancellationToken">Propagates notification that the operation should be cancelled.</param>
    /// <returns>The matching <see cref="PromptTemplate"/>.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no document with the given <paramref name="templateType"/> exists in the container.
    /// The caller must fail the pipeline run immediately — no generator agent should be invoked.
    /// </exception>
    Task<PromptTemplate> GetAsync(string templateType, CancellationToken cancellationToken = default);
}
