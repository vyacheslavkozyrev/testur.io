using Testurio.Core.Entities;
using Testurio.Core.Models;

namespace Testurio.Core.Interfaces;

/// <summary>
/// Contract for the AgentRouter pipeline stage (stage 2).
/// Classifies a <see cref="ParsedStory"/> into the applicable set of test types,
/// filters the result against the project's configured test types, and returns
/// an <see cref="AgentRouterResult"/> for the orchestrator.
/// When no test types remain after filtering, the router posts a skip comment to the
/// originating PM tool ticket and marks the run accordingly.
/// </summary>
public interface IAgentRouter
{
    /// <summary>
    /// Routes the <paramref name="parsedStory"/> to the applicable generator agents.
    /// </summary>
    /// <param name="parsedStory">Structured story output from stage 1 (StoryParser).</param>
    /// <param name="project">Project configuration, including <c>TestingStrategy</c> and PM tool credentials.</param>
    /// <param name="testRun">Current test run record — updated with routing metadata before returning.</param>
    /// <param name="ct">Cancellation token forwarded to all async I/O operations.</param>
    /// <returns>
    /// An <see cref="AgentRouterResult"/> whose <c>ResolvedTestTypes</c> is empty when the run
    /// should be skipped, or contains one or more types when generators should be invoked.
    /// </returns>
    Task<AgentRouterResult> RouteAsync(
        ParsedStory parsedStory,
        Project project,
        TestRun testRun,
        CancellationToken ct = default);
}
