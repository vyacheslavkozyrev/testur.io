using Testurio.Core.Models;

namespace Testurio.Pipeline.StoryParser;

/// <summary>
/// Rule-based check: determines whether a <see cref="WorkItem"/> already conforms to the Testurio story template.
/// A conformant story has a non-empty title, a non-empty description, and at least one acceptance criterion.
/// No I/O or external calls are made — this runs entirely in memory.
/// </summary>
public sealed class TemplateChecker
{
    /// <summary>
    /// Returns <c>true</c> when the work item passes the template check (direct-parse path).
    /// Returns <c>false</c> when any required section is missing or empty (AI-conversion path).
    /// </summary>
    public bool IsConformant(WorkItem workItem)
    {
        if (string.IsNullOrWhiteSpace(workItem.Title))
            return false;

        if (string.IsNullOrWhiteSpace(workItem.Description))
            return false;

        if (string.IsNullOrWhiteSpace(workItem.AcceptanceCriteria))
            return false;

        return true;
    }
}
