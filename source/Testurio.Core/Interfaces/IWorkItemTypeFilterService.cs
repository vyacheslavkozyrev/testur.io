using Testurio.Core.Entities;

namespace Testurio.Core.Interfaces;

public interface IWorkItemTypeFilterService
{
    /// <summary>
    /// Returns true when <paramref name="issueType"/> is in the project's allowed list
    /// (or in the PM-tool default if the project has no explicit list).
    /// Comparison is exact and case-sensitive (AC-013).
    /// </summary>
    bool IsAllowed(Project project, string issueType);

    /// <summary>Returns the effective allowed types: the configured list, or the PM-tool default.</summary>
    string[] GetEffectiveTypes(Project project);
}
