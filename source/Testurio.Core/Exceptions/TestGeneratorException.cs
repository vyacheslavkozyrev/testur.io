using Testurio.Core.Enums;

namespace Testurio.Core.Exceptions;

/// <summary>
/// Thrown by a generator agent (<c>ApiTestGeneratorAgent</c> or <c>UiE2eTestGeneratorAgent</c>)
/// after all retry attempts are exhausted and Claude's response could not be parsed
/// into a valid scenario list.
/// <para>
/// Caught per-agent by <c>TestRunJobProcessor</c>: the failing agent's warning is appended to
/// <c>TestRun.GenerationWarnings</c> and the pipeline continues with an empty list for that type.
/// The other agent's task is not cancelled.
/// </para>
/// </summary>
public sealed class TestGeneratorException : Exception
{
    /// <summary>The test type whose generation failed.</summary>
    public TestType TestType { get; }

    /// <summary>Total number of attempts made (maximum 4).</summary>
    public int Attempts { get; }

    /// <summary>
    /// The raw string returned by Claude on the final attempt, truncated to 2 000 characters.
    /// Included in structured logs for diagnostics; never written to Cosmos or returned to clients.
    /// </summary>
    public string LastRawResponse { get; }

    /// <summary>
    /// Initialises a new <see cref="TestGeneratorException"/>.
    /// </summary>
    /// <param name="testType">The test type that failed.</param>
    /// <param name="attempts">Number of attempts made (1–4).</param>
    /// <param name="lastRawResponse">Claude's final raw response, truncated to 2 000 characters.</param>
    /// <param name="innerException">The <see cref="System.Text.Json.JsonException"/> that triggered the final failure.</param>
    public TestGeneratorException(
        TestType testType,
        int attempts,
        string lastRawResponse,
        Exception innerException)
        : base(
            $"Test generation failed for type '{testType}' after {attempts} attempt(s). " +
            $"Last JSON parse error: {innerException.Message}",
            innerException)
    {
        TestType = testType;
        Attempts = attempts;
        LastRawResponse = lastRawResponse;
    }
}
