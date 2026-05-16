namespace Testurio.Core.Interfaces;

/// <summary>
/// Marker interface for test generator agents.
/// Concrete implementations (<c>ApiTestGeneratorAgent</c>, <c>UiE2eTestGeneratorAgent</c>)
/// are provided by feature 0028 (Testurio.Pipeline.Generators).
/// The router builds a list of agents via <see cref="ITestGeneratorFactory"/> and passes them
/// to the orchestrator for parallel execution in stage 4.
/// </summary>
public interface ITestGeneratorAgent
{
}
