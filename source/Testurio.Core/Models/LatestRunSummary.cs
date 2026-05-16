using Testurio.Core.Enums;

namespace Testurio.Core.Models;

/// <summary>
/// Summarises the most recent test run for a project, used in the dashboard snapshot.
/// </summary>
public record LatestRunSummary(
    string RunId,
    RunStatus Status,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt);
