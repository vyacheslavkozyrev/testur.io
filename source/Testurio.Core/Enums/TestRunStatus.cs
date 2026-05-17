namespace Testurio.Core.Enums;

public enum TestRunStatus
{
    Pending,
    Active,
    Completed,
    Failed,
    Skipped,
    ReportDeliveryFailed,

    /// <summary>
    /// Set by <c>TestRunJobProcessor</c> when <c>ReportWriterException</c> is thrown by stage 6.
    /// Indicates the execution completed but the report could not be generated or persisted.
    /// The Service Bus message is abandoned (not dead-lettered) so the run will be retried.
    /// </summary>
    ReportFailed
}
