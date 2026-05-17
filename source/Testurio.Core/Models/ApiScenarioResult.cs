namespace Testurio.Core.Models;

/// <summary>
/// The outcome of executing a single <see cref="ApiTestScenario"/> via <c>HttpExecutor</c>.
/// All assertion results are collected regardless of individual assertion outcomes —
/// no assertion is skipped when a preceding assertion fails.
/// Contains no execution logic — it is a plain data container.
/// </summary>
public sealed record ApiScenarioResult
{
    /// <summary>UUID v4 identifier matching the originating <see cref="ApiTestScenario.Id"/>.</summary>
    public required string ScenarioId { get; init; }

    /// <summary>Human-readable title copied from the originating <see cref="ApiTestScenario.Title"/>.</summary>
    public required string Title { get; init; }

    /// <summary>
    /// <c>true</c> only when every <see cref="AssertionResult"/> in <see cref="AssertionResults"/>
    /// has <see cref="AssertionResult.Passed"/> equal to <c>true</c>.
    /// </summary>
    public required bool Passed { get; init; }

    /// <summary>
    /// Elapsed time in milliseconds from the moment the HTTP request was sent to the moment
    /// the full response body was received. Set to <c>0</c> when the request failed before
    /// a response could be obtained.
    /// </summary>
    public required long DurationMs { get; init; }

    /// <summary>
    /// Results for each assertion declared in the scenario.
    /// Never <c>null</c>; every assertion in the original scenario has a corresponding entry.
    /// </summary>
    public required IReadOnlyList<AssertionResult> AssertionResults { get; init; }
}

/// <summary>
/// The outcome of evaluating a single assertion within an <see cref="ApiTestScenario"/>.
/// Contains no execution logic — it is a plain data container.
/// </summary>
public sealed record AssertionResult
{
    /// <summary>
    /// Assertion discriminator matching the source <see cref="Assertion.Type"/>
    /// (<c>"status_code"</c>, <c>"json_path"</c>, or <c>"header"</c>).
    /// </summary>
    public required string Type { get; init; }

    /// <summary>
    /// <c>true</c> when the actual response value matched the expected value according to
    /// the assertion's evaluation rules.
    /// </summary>
    public required bool Passed { get; init; }

    /// <summary>Expected value as declared in the scenario. Never <c>null</c>.</summary>
    public required string Expected { get; init; }

    /// <summary>
    /// Actual value extracted from the HTTP response. Always populated — never <c>null</c>.
    /// Set to the exception message when the HTTP request itself failed.
    /// </summary>
    public required string Actual { get; init; }
}
