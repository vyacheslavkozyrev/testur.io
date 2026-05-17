namespace Testurio.Core.Models;

/// <summary>
/// Plain data container that carries the merged output of stage 5 (ExecutorRouter / feature 0029)
/// into stage 6 (ReportWriter / feature 0030).
/// <para>
/// Produced by <see cref="Testurio.Core.Interfaces.IExecutorRouter"/> after
/// <c>Task.WhenAll</c> resolves across <c>HttpExecutor</c> and <c>PlaywrightExecutor</c>.
/// Either list is empty when the corresponding executor was not invoked.
/// Neither list is ever <c>null</c>.
/// </para>
/// Contains no execution logic — it is a plain data handoff record.
/// </summary>
public sealed record ExecutionResult
{
    /// <summary>
    /// Results of API scenario execution produced by <c>HttpExecutor</c>.
    /// Empty when the <c>api</c> test type was not enabled or when <c>ApiScenarios</c> was empty.
    /// Never <c>null</c>.
    /// </summary>
    public required IReadOnlyList<ApiScenarioResult> ApiResults { get; init; }

    /// <summary>
    /// Results of UI end-to-end scenario execution produced by <c>PlaywrightExecutor</c>.
    /// Empty when the <c>ui_e2e</c> test type was not enabled or when <c>UiE2eScenarios</c> was empty.
    /// Never <c>null</c>.
    /// </summary>
    public required IReadOnlyList<UiE2eScenarioResult> UiE2eResults { get; init; }
}
