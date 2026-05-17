using Testurio.Api.DTOs;
using Testurio.Core.Interfaces;

namespace Testurio.Api.Services;

public interface IProjectHistoryService
{
    /// <summary>
    /// Returns the run history and 90-day trend points for the given project.
    /// Returns <c>null</c> when the project does not exist or is not owned by <paramref name="userId"/>.
    /// </summary>
    Task<ProjectHistoryResponse?> GetHistoryAsync(
        string userId,
        string projectId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the full run detail for a single test run.
    /// Returns <c>null</c> when the run does not exist, the project does not match, or the user does not own the project.
    /// </summary>
    Task<RunDetailResponse?> GetRunDetailAsync(
        string userId,
        string projectId,
        string runId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Delegates run-history and run-detail queries to <see cref="IStatsRepository"/>
/// and maps domain models to API response DTOs.
/// </summary>
public class ProjectHistoryService : IProjectHistoryService
{
    private readonly IStatsRepository _statsRepository;

    public ProjectHistoryService(IStatsRepository statsRepository)
    {
        _statsRepository = statsRepository;
    }

    /// <inheritdoc/>
    public async Task<ProjectHistoryResponse?> GetHistoryAsync(
        string userId,
        string projectId,
        CancellationToken cancellationToken = default)
    {
        var result = await _statsRepository.GetProjectHistoryAsync(userId, projectId, cancellationToken);

        if (result is null)
            return null;

        return new ProjectHistoryResponse(result.Value.Runs, result.Value.TrendPoints);
    }

    /// <inheritdoc/>
    public async Task<RunDetailResponse?> GetRunDetailAsync(
        string userId,
        string projectId,
        string runId,
        CancellationToken cancellationToken = default)
    {
        var testResult = await _statsRepository.GetRunDetailAsync(userId, projectId, runId, cancellationToken);

        if (testResult is null)
            return null;

        return new RunDetailResponse(
            Id: testResult.Id,
            RunId: testResult.RunId,
            StoryTitle: testResult.StoryTitle,
            Verdict: testResult.Verdict,
            Recommendation: testResult.Recommendation,
            TotalDurationMs: testResult.TotalDurationMs,
            CreatedAt: testResult.CreatedAt,
            ScenarioResults: testResult.ScenarioResults,
            RawCommentMarkdown: testResult.RawCommentMarkdown);
    }
}
