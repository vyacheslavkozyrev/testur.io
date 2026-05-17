namespace Testurio.Core.Models;

/// <summary>
/// The outcome of executing a single <see cref="UiE2eTestScenario"/> via <c>PlaywrightExecutor</c>.
/// Steps are always executed in order; when a step fails, all remaining steps are recorded as skipped.
/// Contains no execution logic — it is a plain data container.
/// </summary>
public sealed record UiE2eScenarioResult
{
    /// <summary>UUID v4 identifier matching the originating <see cref="UiE2eTestScenario.Id"/>.</summary>
    public required string ScenarioId { get; init; }

    /// <summary>Human-readable title copied from the originating <see cref="UiE2eTestScenario.Title"/>.</summary>
    public required string Title { get; init; }

    /// <summary>
    /// <c>true</c> only when every <see cref="StepExecutionResult"/> in <see cref="StepResults"/>
    /// has <see cref="StepExecutionResult.Passed"/> equal to <c>true</c>.
    /// </summary>
    public required bool Passed { get; init; }

    /// <summary>
    /// Elapsed time in milliseconds from the first step start to the last step end (or failure)
    /// for this scenario.
    /// </summary>
    public required long DurationMs { get; init; }

    /// <summary>
    /// Results for each step in the scenario, in execution order.
    /// Never <c>null</c>; every step in the original scenario has a corresponding entry.
    /// </summary>
    public required IReadOnlyList<StepExecutionResult> StepResults { get; init; }
}

/// <summary>
/// The outcome of executing a single step within a <see cref="UiE2eTestScenario"/>.
/// Contains no execution logic — it is a plain data container.
/// </summary>
public sealed record StepExecutionResult
{
    /// <summary>Zero-based index of the step within the scenario's step list.</summary>
    public required int StepIndex { get; init; }

    /// <summary>
    /// Action discriminator matching the source <see cref="UiStep.Action"/>
    /// (<c>"navigate"</c>, <c>"click"</c>, <c>"fill"</c>, <c>"assert_visible"</c>,
    /// <c>"assert_text"</c>, or <c>"assert_url"</c>).
    /// </summary>
    public required string Action { get; init; }

    /// <summary><c>true</c> when the step completed without error.</summary>
    public required bool Passed { get; init; }

    /// <summary>
    /// Error message when <see cref="Passed"/> is <c>false</c>.
    /// <c>"Skipped — preceding step failed"</c> for steps that were not reached due to an
    /// earlier step failure. <c>null</c> when <see cref="Passed"/> is <c>true</c>.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Full Blob Storage URI of the captured screenshot PNG, or <c>null</c>.
    /// Non-null only for failed assertion steps (<c>assert_visible</c>, <c>assert_text</c>,
    /// <c>assert_url</c>) where the Blob upload succeeded.
    /// All other steps (passed or non-assertion) always have <c>null</c> here.
    /// </summary>
    public string? ScreenshotBlobUri { get; init; }
}
