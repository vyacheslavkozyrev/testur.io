using System.ComponentModel.DataAnnotations;

namespace Testurio.Api.DTOs;

/// <summary>
/// Response DTO for prompt template read operations (GET /v1/admin/prompt-templates/{stage}).
/// </summary>
public sealed record PromptTemplateDto(
    string Stage,
    int Version,
    string Body,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <summary>
/// Request body for prompt template update operations (PUT /v1/admin/prompt-templates/{stage}).
/// </summary>
public sealed class UpdatePromptTemplateRequest
{
    /// <summary>
    /// The new prompt body text. Must be non-empty.
    /// </summary>
    [Required]
    [MinLength(1)]
    public required string Body { get; init; }
}
