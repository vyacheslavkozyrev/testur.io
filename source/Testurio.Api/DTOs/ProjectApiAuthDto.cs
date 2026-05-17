using System.ComponentModel.DataAnnotations;

namespace Testurio.Api.DTOs;

/// <summary>
/// Response body for GET and PATCH /v1/projects/{projectId}/api-auth.
/// Never exposes secret values — only method, non-secret fields, and configured flags.
/// </summary>
public sealed record ProjectApiAuthDto(
    string ProjectId,
    /// <summary>"none" | "bearer" | "api_key" | "basic"</summary>
    string ApiAuthMethod,
    /// <summary>True when method is "bearer" and a token has been stored. Null otherwise.</summary>
    bool? ApiAuthBearerTokenConfigured,
    /// <summary>Pre-filled key name when method is "api_key". Null otherwise.</summary>
    string? ApiAuthApiKeyName,
    /// <summary>"header" | "query" when method is "api_key". Null otherwise.</summary>
    string? ApiAuthApiKeyPlacement,
    /// <summary>True when method is "api_key" and a value has been stored. Null otherwise.</summary>
    bool? ApiAuthApiKeyValueConfigured,
    /// <summary>Pre-filled username when method is "basic". Null otherwise.</summary>
    string? ApiAuthBasicUsername,
    /// <summary>True when method is "basic" and a password has been stored. Null otherwise.</summary>
    bool? ApiAuthBasicPasswordConfigured);

/// <summary>
/// Request body for PATCH /v1/projects/{projectId}/api-auth.
/// Conditional required fields depend on the selected method.
/// </summary>
public sealed class UpdateProjectApiAuthRequest : IValidatableObject
{
    [Required]
    [AllowedValues("none", "bearer", "api_key", "basic")]
    public string ApiAuthMethod { get; init; } = "none";

    [MaxLength(5000)]
    public string? ApiAuthBearerToken { get; init; }

    [MaxLength(200)]
    public string? ApiAuthApiKeyName { get; init; }

    public string? ApiAuthApiKeyPlacement { get; init; }

    [MaxLength(5000)]
    public string? ApiAuthApiKeyValue { get; init; }

    [MaxLength(200)]
    public string? ApiAuthBasicUsername { get; init; }

    [MaxLength(5000)]
    public string? ApiAuthBasicPassword { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (ApiAuthMethod == "bearer")
        {
            if (string.IsNullOrWhiteSpace(ApiAuthBearerToken))
                yield return new ValidationResult(
                    "Token is required when apiAuthMethod is 'bearer'.", [nameof(ApiAuthBearerToken)]);
        }

        if (ApiAuthMethod == "api_key")
        {
            if (string.IsNullOrWhiteSpace(ApiAuthApiKeyName))
                yield return new ValidationResult(
                    "Key name is required when apiAuthMethod is 'api_key'.", [nameof(ApiAuthApiKeyName)]);
            if (string.IsNullOrWhiteSpace(ApiAuthApiKeyPlacement))
                yield return new ValidationResult(
                    "Key placement is required when apiAuthMethod is 'api_key'.", [nameof(ApiAuthApiKeyPlacement)]);
            else if (ApiAuthApiKeyPlacement != "header" && ApiAuthApiKeyPlacement != "query")
                yield return new ValidationResult(
                    "Key placement must be 'header' or 'query'.", [nameof(ApiAuthApiKeyPlacement)]);
            if (string.IsNullOrWhiteSpace(ApiAuthApiKeyValue))
                yield return new ValidationResult(
                    "Key value is required when apiAuthMethod is 'api_key'.", [nameof(ApiAuthApiKeyValue)]);
        }

        if (ApiAuthMethod == "basic")
        {
            if (string.IsNullOrWhiteSpace(ApiAuthBasicUsername))
                yield return new ValidationResult(
                    "Username is required when apiAuthMethod is 'basic'.", [nameof(ApiAuthBasicUsername)]);
            if (string.IsNullOrWhiteSpace(ApiAuthBasicPassword))
                yield return new ValidationResult(
                    "Password is required when apiAuthMethod is 'basic'.", [nameof(ApiAuthBasicPassword)]);
        }
    }
}
