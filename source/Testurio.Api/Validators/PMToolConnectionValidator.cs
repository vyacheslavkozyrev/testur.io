using System.ComponentModel.DataAnnotations;

namespace Testurio.Api.Validators;

/// <summary>
/// Shared validation helpers for PM tool connection requests.
/// </summary>
internal static class PMToolConnectionValidator
{
    /// <summary>
    /// Validates that a string is a well-formed absolute URL with http or https scheme.
    /// </summary>
    public static ValidationResult? ValidateUrl(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
            return new ValidationResult($"{fieldName} is required.", [fieldName]);

        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return new ValidationResult(
                $"{fieldName} must be a valid URL starting with http:// or https://.",
                [fieldName]);
        }

        return ValidationResult.Success;
    }
}
