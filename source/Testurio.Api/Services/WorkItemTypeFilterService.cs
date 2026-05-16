using Testurio.Core.Entities;
using Testurio.Core.Interfaces;

namespace Testurio.Api.Services;

public sealed class WorkItemTypeFilterService : IWorkItemTypeFilterService
{
    public bool IsAllowed(Project project, string issueType)
    {
        var effective = GetEffectiveTypes(project);
        return effective.Contains(issueType, StringComparer.Ordinal);
    }

    public string[] GetEffectiveTypes(Project project)
    {
        if (project.AllowedWorkItemTypes is { Length: > 0 })
            return project.AllowedWorkItemTypes;

        return project.PmTool.HasValue
            ? Project.GetDefaultAllowedWorkItemTypes(project.PmTool.Value)
            : ["Story", "Bug"];
    }
}
