using System.ComponentModel.DataAnnotations;

namespace Testurio.Api.DTOs;

/// <summary>
/// Request body for PATCH /v1/projects/{projectId}/work-item-type-filter (AC-017).
/// </summary>
public sealed record UpdateWorkItemTypeFilterRequest(
    [property: Required]
    [property: MinLength(1, ErrorMessage = "At least one work item type must be selected")]
    [property: MaxLength(20, ErrorMessage = "A maximum of 20 work item types may be configured")]
    [property: NoEmptyStrings(ErrorMessage = "Work item type values must be non-empty strings")]
    string[] AllowedWorkItemTypes);

/// <summary>Validates that no element in the string array is null or whitespace.</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class NoEmptyStringsAttribute : ValidationAttribute
{
    public override bool IsValid(object? value) =>
        value is string[] arr && arr.All(s => !string.IsNullOrWhiteSpace(s));
}
