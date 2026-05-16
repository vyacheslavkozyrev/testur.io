using Microsoft.Extensions.Logging;
using Testurio.Core.Entities;
using Testurio.Core.Enums;
using Testurio.Core.Interfaces;
using Testurio.Core.Models;
using Testurio.Core.Repositories;

namespace Testurio.Pipeline.AgentRouter;

/// <summary>
/// Implements the AgentRouter pipeline stage (stage 2).
/// Classifies the parsed story into applicable test types, filters against the project config,
/// posts a skip comment when no types remain, and builds the generator list for stage 4.
/// </summary>
public sealed partial class AgentRouterService : IAgentRouter
{
    /// <summary>
    /// MVP test types — the complete set of types the router can produce.
    /// Post-MVP types are out of scope and must not be returned.
    /// </summary>
    private static readonly TestType[] AllMvpTypes = [TestType.Api, TestType.UiE2e];

    private readonly StoryClassifier _classifier;
    private readonly SkipCommentPoster _skipCommentPoster;
    private readonly ITestGeneratorFactory _generatorFactory;
    private readonly ITestRunRepository _testRunRepository;
    private readonly ILogger<AgentRouterService> _logger;

    public AgentRouterService(
        StoryClassifier classifier,
        SkipCommentPoster skipCommentPoster,
        ITestGeneratorFactory generatorFactory,
        ITestRunRepository testRunRepository,
        ILogger<AgentRouterService> logger)
    {
        _classifier = classifier;
        _skipCommentPoster = skipCommentPoster;
        _generatorFactory = generatorFactory;
        _testRunRepository = testRunRepository;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<AgentRouterResult> RouteAsync(
        ParsedStory parsedStory,
        Project project,
        TestRun testRun,
        CancellationToken ct = default)
    {
        // Step 1: Classify the story using Claude.
        var (suggestedTypes, reason) = await _classifier.ClassifyAsync(parsedStory, ct);
        LogClassified(_logger, testRun.Id, string.Join(", ", suggestedTypes), reason);

        // Step 2: Determine which test types are enabled for this project.
        // Null/empty TestTypes defaults to all MVP types for backwards compatibility.
        var projectTypes = project.TestTypes is { Length: > 0 }
            ? project.TestTypes
            : AllMvpTypes;

        // Step 3: Filter Claude's suggestions against the project-configured types.
        var resolved = suggestedTypes
            .Where(t => projectTypes.Contains(t))
            .Distinct()
            .ToArray();

        LogFiltered(_logger, testRun.Id, string.Join(", ", resolved));

        // Step 4: Build the result with routing metadata.
        var result = new AgentRouterResult
        {
            ResolvedTestTypes = resolved,
            ClassificationReason = reason
        };

        // Step 5: Persist routing metadata to the run record before returning.
        testRun.ResolvedTestTypes = resolved.Select(t => t.ToString()).ToArray();
        testRun.ClassificationReason = reason;

        if (resolved.Length == 0)
        {
            // AC-009: skip path — mark the run as Skipped and post the comment.
            testRun.Status = TestRunStatus.Skipped;
            testRun.SkipReason = "Skipped — no applicable test type";
            await _testRunRepository.UpdateAsync(testRun, ct);

            LogSkipped(_logger, testRun.Id, reason);

            // AC-008: comment post is fire-and-forget — failure must not propagate.
            await _skipCommentPoster.PostSkipCommentAsync(testRun, project, reason, ct);
        }
        else
        {
            // Normal path — persist routing metadata. The caller will continue to stage 4.
            await _testRunRepository.UpdateAsync(testRun, ct);

            // Build generator instances to verify the factory can resolve each type.
            // The orchestrator receives these via AgentRouterResult for parallel execution.
            foreach (var testType in resolved)
                _ = _generatorFactory.Create(testType);
        }

        return result;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "AgentRouter: run {RunId} classified as [{Types}] — reason: {Reason}")]
    private static partial void LogClassified(ILogger logger, string runId, string types, string reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "AgentRouter: run {RunId} resolved to [{Types}] after project-config filter")]
    private static partial void LogFiltered(ILogger logger, string runId, string types);

    [LoggerMessage(Level = LogLevel.Warning, Message = "AgentRouter: run {RunId} skipped — no applicable test type. Reason: {Reason}")]
    private static partial void LogSkipped(ILogger logger, string runId, string reason);
}
