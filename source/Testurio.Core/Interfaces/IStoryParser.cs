using Testurio.Core.Exceptions;
using Testurio.Core.Models;

namespace Testurio.Core.Interfaces;

/// <summary>
/// Contract for the StoryParser pipeline stage (stage 1).
/// Converts a raw PM tool work item into a structured <see cref="ParsedStory"/>.
/// Throws <see cref="StoryParserException"/> on unrecoverable failure.
/// </summary>
public interface IStoryParser
{
    /// <summary>
    /// Parses the given <paramref name="workItem"/> into a <see cref="ParsedStory"/>.
    /// Uses direct rule-based parsing when the work item conforms to the Testurio template,
    /// or calls the Claude API to convert it when it does not.
    /// </summary>
    /// <remarks>
    /// This overload does not post a PM tool warning comment when AI conversion is used,
    /// because the <c>Project</c> credential context is not available through this interface.
    /// When comment posting is required (e.g. in the Worker pipeline), resolve
    /// <c>StoryParserService</c> directly and call its
    /// <c>ParseAsync(WorkItem, Project?, CancellationToken)</c> overload.
    /// </remarks>
    /// <exception cref="StoryParserException">
    /// Thrown when AI conversion is required but the Claude API response is malformed or missing required fields.
    /// </exception>
    Task<ParsedStory> ParseAsync(WorkItem workItem, CancellationToken ct = default);
}
