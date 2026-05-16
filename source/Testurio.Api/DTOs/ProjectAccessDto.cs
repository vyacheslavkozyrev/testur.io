using System.ComponentModel.DataAnnotations;
using Testurio.Core.Enums;

namespace Testurio.Api.DTOs;

/// <summary>
/// Request body for PATCH /v1/projects/{projectId}/access.
/// Conditional required fields depend on the selected access mode.
/// </summary>
public sealed class UpdateProjectAccessRequest : IValidatableObject
{
    [Required]
    public AccessMode AccessMode { get; init; }

    [MaxLength(200)]
    public string? BasicAuthUser { get; init; }

    [MaxLength(500)]
    public string? BasicAuthPass { get; init; }

    [MaxLength(200)]
    [RegularExpression(@"^[A-Za-z0-9\-]+$",
        ErrorMessage = "Header name must contain only alphanumeric characters and hyphens.")]
    public string? HeaderTokenName { get; init; }

    [MaxLength(1000)]
    public string? HeaderTokenValue { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!Enum.IsDefined(typeof(AccessMode), AccessMode))
        {
            yield return new ValidationResult(
                $"'{AccessMode}' is not a valid access mode.", [nameof(AccessMode)]);
            yield break;
        }

        if (AccessMode == AccessMode.BasicAuth)
        {
            if (string.IsNullOrWhiteSpace(BasicAuthUser))
                yield return new ValidationResult(
                    "Username is required for HTTP Basic Auth.", [nameof(BasicAuthUser)]);
            // BasicAuthPass is optional on re-save — omitting it keeps the existing Key Vault secret.
        }

        if (AccessMode == AccessMode.HeaderToken)
        {
            if (string.IsNullOrWhiteSpace(HeaderTokenName))
                yield return new ValidationResult(
                    "Header name is required for Custom Header Token.", [nameof(HeaderTokenName)]);
            // HeaderTokenValue is optional on re-save — omitting it keeps the existing Key Vault secret.
        }
    }
}

/// <summary>
/// Safe response body for GET and PATCH /v1/projects/{projectId}/access.
/// Never exposes plaintext credentials — only the mode, optional username, and optional header name.
/// </summary>
public sealed record ProjectAccessDto(
    string ProjectId,
    AccessMode AccessMode,
    /// <summary>Pre-filled username when mode is BasicAuth; null otherwise.</summary>
    string? BasicAuthUser,
    /// <summary>Pre-filled header name when mode is HeaderToken; null otherwise.</summary>
    string? HeaderTokenName);
