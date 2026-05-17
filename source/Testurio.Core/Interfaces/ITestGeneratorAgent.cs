using Testurio.Core.Models;

namespace Testurio.Core.Interfaces;

/// <summary>
/// Contract for a pipeline stage-4 test generator agent.
/// Concrete implementations (<c>ApiTestGeneratorAgent</c>, <c>UiE2eTestGeneratorAgent</c>)
/// are provided by feature 0028 (Testurio.Pipeline.Generators) and registered in DI with
/// the keyed service keys <c>"api"</c> and <c>"ui_e2e"</c> respectively.
/// The router builds a list of agents via <see cref="ITestGeneratorFactory"/> and passes them
/// to the orchestrator for parallel execution in stage 4.
/// </summary>
public interface ITestGeneratorAgent
{
    /// <summary>
    /// Generates test scenarios for the given <paramref name="context"/> by calling Claude
    /// with adaptive thinking enabled, parsing the streamed response into typed scenario records,
    /// and returning them as a <see cref="GeneratorResults"/>.
    /// </summary>
    /// <param name="context">
    /// All inputs required for generation: parsed story, memory examples, project config,
    /// prompt template, and run identifier.
    /// </param>
    /// <param name="ct">
    /// Cancellation token forwarded to the Claude streaming call.
    /// Cancelling this token causes any in-flight LLM call to be aborted.
    /// </param>
    /// <returns>
    /// A <see cref="GeneratorResults"/> with either <see cref="GeneratorResults.ApiScenarios"/>
    /// or <see cref="GeneratorResults.UiE2eScenarios"/> populated (the other list is always empty).
    /// </returns>
    /// <exception cref="Exceptions.TestGeneratorException">
    /// Thrown after all retry attempts are exhausted and Claude's output could not be parsed
    /// into a valid scenario list.
    /// </exception>
    Task<GeneratorResults> GenerateAsync(GeneratorContext context, CancellationToken ct);
}
