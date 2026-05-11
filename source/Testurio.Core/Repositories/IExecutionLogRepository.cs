using Testurio.Core.Entities;

namespace Testurio.Core.Repositories;

public interface IExecutionLogRepository
{
    /// <summary>Persists a single log entry.</summary>
    Task PersistAsync(ExecutionLogEntry entry, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns all log entries associated with the given run, ordered by step index ascending.
    /// </summary>
    Task<IReadOnlyList<ExecutionLogEntry>> GetByRunAsync(
        string projectId,
        string testRunId,
        CancellationToken cancellationToken = default);

    /// <summary>Returns the log entry for a specific step within a run, or null if not found.</summary>
    Task<ExecutionLogEntry?> GetByStepAsync(
        string projectId,
        string testRunId,
        string scenarioId,
        int stepIndex,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes all log entries for the given run.
    /// Called when the parent run record is deleted (AC-010).
    /// </summary>
    Task DeleteByRunAsync(
        string projectId,
        string testRunId,
        CancellationToken cancellationToken = default);
}
