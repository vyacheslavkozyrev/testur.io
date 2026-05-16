using Testurio.Core.Enums;

namespace Testurio.Core.Models;

/// <summary>
/// PM-tool-agnostic representation of a work item delivered to the StoryParser stage.
/// Carries enough context for both parsing and PM tool comment posting.
/// </summary>
public class WorkItem
{
    /// <summary>Raw title of the work item (e.g. Jira issue summary or ADO work item title).</summary>
    public required string Title { get; init; }

    /// <summary>Raw description body of the work item.</summary>
    public required string Description { get; init; }

    /// <summary>Raw acceptance criteria text. May be empty when the field is absent.</summary>
    public string AcceptanceCriteria { get; init; } = string.Empty;

    /// <summary>Which PM tool originated this work item.</summary>
    public required PMToolType PmToolType { get; init; }

    /// <summary>
    /// Issue key used to post comments back (e.g. "PROJ-42" for Jira, or the work item id string for ADO).
    /// </summary>
    public required string IssueKey { get; init; }

    /// <summary>Numeric work item id for ADO comment posting. Null when PmToolType is Jira.</summary>
    public int? AdoWorkItemId { get; init; }
}
