namespace Testurio.Core.Models;

/// <summary>
/// Plain data container that carries the typed output of stage 4 (test generator agents)
/// into stage 5 (ExecutorRouter / feature 0029).
/// <para>
/// Produced after <c>Task.WhenAll</c> resolves in <c>TestRunJobProcessor</c>:
/// the <see cref="ApiScenarios"/> list is populated by <c>ApiTestGeneratorAgent</c>,
/// and the <see cref="UiE2eScenarios"/> list is populated by <c>UiE2eTestGeneratorAgent</c>.
/// Either list may be empty when the corresponding agent was not invoked (test type not enabled)
/// or when the agent exhausted its retry budget and produced a <c>GenerationWarnings</c> entry.
/// </para>
/// Contains no formatting or execution logic — it is a plain data handoff record.
/// </summary>
public sealed record GeneratorResults
{
    /// <summary>
    /// API test scenarios produced by <c>ApiTestGeneratorAgent</c>.
    /// Empty when the <c>api</c> test type was not enabled or the agent failed after retries.
    /// </summary>
    public required IReadOnlyList<ApiTestScenario> ApiScenarios { get; init; }

    /// <summary>
    /// UI end-to-end test scenarios produced by <c>UiE2eTestGeneratorAgent</c>.
    /// Empty when the <c>ui_e2e</c> test type was not enabled or the agent failed after retries.
    /// </summary>
    public required IReadOnlyList<UiE2eTestScenario> UiE2eScenarios { get; init; }
}
