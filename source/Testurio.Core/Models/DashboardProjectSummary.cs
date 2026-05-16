namespace Testurio.Core.Models;

/// <summary>
/// Represents a single project card in the dashboard snapshot.
/// <see cref="LatestRun"/> is null when the project has no test runs yet.
/// </summary>
public record DashboardProjectSummary(
    string ProjectId,
    string Name,
    string ProductUrl,
    string TestingStrategy,
    LatestRunSummary? LatestRun);
