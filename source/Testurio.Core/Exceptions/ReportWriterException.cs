namespace Testurio.Core.Exceptions;

/// <summary>
/// Thrown by <c>ReportWriter</c> on unrecoverable stage-6 failures:
/// <list type="bullet">
///   <item>Claude fails to produce a valid JSON report after the retry budget is exhausted</item>
///   <item>The verdict invariant check fails (Claude's verdict disagrees with the raw <c>ExecutionResult</c>)</item>
///   <item>The Cosmos DB write of the <c>TestResult</c> document fails</item>
/// </list>
/// <para>
/// Caught by <c>TestRunJobProcessor</c>: <c>TestRun.Status</c> is set to <c>ReportFailed</c>,
/// the status is persisted via <c>ITestRunRepository</c>, and the exception is re-thrown so
/// the Service Bus message is abandoned and retried (or eventually dead-lettered).
/// </para>
/// </summary>
public sealed class ReportWriterException : Exception
{
    /// <summary>
    /// Initialises a new <see cref="ReportWriterException"/> with the specified message.
    /// </summary>
    /// <param name="message">Human-readable description of the failure reason.</param>
    public ReportWriterException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initialises a new <see cref="ReportWriterException"/> with the specified message
    /// and a reference to the inner exception that caused this exception.
    /// </summary>
    /// <param name="message">Human-readable description of the failure reason.</param>
    /// <param name="innerException">The exception that is the cause of this exception.</param>
    public ReportWriterException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
