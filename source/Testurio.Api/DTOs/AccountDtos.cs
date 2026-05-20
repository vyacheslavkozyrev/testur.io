using System.ComponentModel.DataAnnotations;

namespace Testurio.Api.DTOs;

/// <summary>Response DTO for <c>GET /v1/account/profile</c> and <c>PATCH /v1/account/profile</c>.</summary>
public record AccountProfileDto(string UserId, string? FirstName, string? LastName);

/// <summary>Response DTO for <c>GET /v1/account/preferences</c> and <c>PATCH /v1/account/preferences</c>.</summary>
public record AccountPreferencesDto(string? Language, string? Theme);

/// <summary>Request body for <c>PATCH /v1/account/profile</c>.</summary>
public class UpdateProfileRequest
{
    [MaxLength(100, ErrorMessage = "First name must be 100 characters or fewer.")]
    public string? FirstName { get; init; }

    [MaxLength(100, ErrorMessage = "Last name must be 100 characters or fewer.")]
    public string? LastName { get; init; }
}

/// <summary>
/// Request body for <c>PATCH /v1/account/preferences</c>.
/// All fields are optional — only the provided fields are merged.
/// </summary>
public class UpdatePreferencesRequest
{
    [AllowedValues("en", "uk", "es", "be", ErrorMessage = "Language must be one of: en, uk, es, be.")]
    public string? Language { get; init; }

    [AllowedValues("light", "dark", ErrorMessage = "Theme must be one of: light, dark.")]
    public string? Theme { get; init; }
}
