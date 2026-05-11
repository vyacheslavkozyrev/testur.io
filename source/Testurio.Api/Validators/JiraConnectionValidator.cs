using System.ComponentModel.DataAnnotations;
using Testurio.Api.DTOs;
using Testurio.Core.Enums;

namespace Testurio.Api.Validators;

/// <summary>
/// Validates a <see cref="SaveJiraConnectionRequest"/> beyond DataAnnotation constraints.
/// Checks that auth-method-specific credential fields are present and the Base URL is valid.
/// </summary>
public static class JiraConnectionValidator
{
    public static IEnumerable<ValidationResult> Validate(SaveJiraConnectionRequest request)
    {
        if (!Uri.TryCreate(request.BaseUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            yield return new ValidationResult(
                "Base URL must be a valid URL starting with http:// or https://.",
                [nameof(request.BaseUrl)]);
        }

        if (request.AuthMethod == JiraAuthMethod.ApiToken)
        {
            if (string.IsNullOrWhiteSpace(request.Email))
            {
                yield return new ValidationResult(
                    "Email is required when API Token auth method is selected.",
                    [nameof(request.Email)]);
            }

            if (string.IsNullOrWhiteSpace(request.ApiToken))
            {
                yield return new ValidationResult(
                    "API Token is required when API Token auth method is selected.",
                    [nameof(request.ApiToken)]);
            }
        }

        if (request.AuthMethod == JiraAuthMethod.Pat && string.IsNullOrWhiteSpace(request.Pat))
        {
            yield return new ValidationResult(
                "Personal Access Token is required when PAT auth method is selected.",
                [nameof(request.Pat)]);
        }
    }
}
