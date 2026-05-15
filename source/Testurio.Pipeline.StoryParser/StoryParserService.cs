using Microsoft.Extensions.Logging;
using Testurio.Core.Entities;
using Testurio.Core.Exceptions;
using Testurio.Core.Interfaces;
using Testurio.Core.Models;

namespace Testurio.Pipeline.StoryParser;

/// <summary>
/// Orchestrates the StoryParser pipeline stage (stage 1).
/// Routes each <see cref="WorkItem"/> through either the direct-parse path or the AI-conversion path,
/// and posts a warning comment to the PM tool ticket when AI conversion is used.
/// Implements <see cref="IStoryParser"/>.
/// </summary>
public sealed partial class StoryParserService : IStoryParser
{
    private readonly TemplateChecker _templateChecker;
    private readonly DirectParser _directParser;
    private readonly AiStoryConverter _aiConverter;
    private readonly PmToolCommentPoster _commentPoster;
    private readonly ILogger<StoryParserService> _logger;

    // Project context is passed per-parse call via RunContext; held here for comment posting.
    // We receive it from the worker via a wrapper that passes project alongside the work item.
    // For the IStoryParser.ParseAsync signature we use the overload that also accepts a project.

    public StoryParserService(
        TemplateChecker templateChecker,
        DirectParser directParser,
        AiStoryConverter aiConverter,
        PmToolCommentPoster commentPoster,
        ILogger<StoryParserService> logger)
    {
        _templateChecker = templateChecker;
        _directParser = directParser;
        _aiConverter = aiConverter;
        _commentPoster = commentPoster;
        _logger = logger;
    }

    /// <inheritdoc />
    /// <remarks>
    /// This overload does not post the PM tool warning comment because the <see cref="Project"/>
    /// context is not available. Use <see cref="ParseAsync(WorkItem, Project, CancellationToken)"/>
    /// from the Worker pipeline stage to get comment posting behaviour.
    /// </remarks>
    public Task<ParsedStory> ParseAsync(WorkItem workItem, CancellationToken ct = default)
        => ParseAsync(workItem, project: null, ct);

    /// <summary>
    /// Parses the <paramref name="workItem"/> and, when AI conversion is used, posts a warning comment
    /// to the PM tool ticket using <paramref name="project"/> credentials.
    /// </summary>
    /// <exception cref="StoryParserException">Thrown when AI conversion is required but fails.</exception>
    public async Task<ParsedStory> ParseAsync(WorkItem workItem, Project? project, CancellationToken ct = default)
    {
        if (_templateChecker.IsConformant(workItem))
        {
            LogDirectParse(_logger, workItem.IssueKey);
            return _directParser.Parse(workItem);
        }

        LogAiConversion(_logger, workItem.IssueKey);

        // AI conversion — throws StoryParserException on unrecoverable failure.
        var parsedStory = await _aiConverter.ConvertAsync(workItem, ct);

        // AC-014: post warning comment asynchronously; failure must not halt the pipeline.
        if (project is not null)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await _commentPoster.PostWarningAsync(workItem, project, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    LogCommentPostError(_logger, workItem.IssueKey, ex);
                }
            });
        }

        return parsedStory;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "StoryParserService: direct parse for issue {IssueKey}")]
    private static partial void LogDirectParse(ILogger logger, string issueKey);

    [LoggerMessage(Level = LogLevel.Information, Message = "StoryParserService: AI conversion for issue {IssueKey}")]
    private static partial void LogAiConversion(ILogger logger, string issueKey);

    [LoggerMessage(Level = LogLevel.Warning, Message = "StoryParserService: warning comment post failed for issue {IssueKey} (non-fatal)")]
    private static partial void LogCommentPostError(ILogger logger, string issueKey, Exception ex);
}
