namespace Testurio.Core.Exceptions;

/// <summary>
/// Thrown by <c>ExecutorRouter</c> when both the <c>ApiScenarios</c> and
/// <c>UiE2eScenarios</c> lists in the incoming <c>GeneratorResults</c> are empty,
/// making it impossible to select any executor.
/// <para>
/// Caught by <c>TestRunJobProcessor</c> as a permanent pipeline failure — the run is
/// marked <c>Failed</c> and the Service Bus message is dead-lettered so that no retry
/// is attempted.
/// </para>
/// </summary>
public sealed class ExecutorRouterException : Exception
{
    /// <summary>
    /// Initialises a new <see cref="ExecutorRouterException"/> with the specified message.
    /// </summary>
    /// <param name="message">Human-readable description of the failure reason.</param>
    public ExecutorRouterException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initialises a new <see cref="ExecutorRouterException"/> with the specified message
    /// and a reference to the inner exception that caused this exception.
    /// </summary>
    /// <param name="message">Human-readable description of the failure reason.</param>
    /// <param name="innerException">The exception that is the cause of this exception.</param>
    public ExecutorRouterException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
