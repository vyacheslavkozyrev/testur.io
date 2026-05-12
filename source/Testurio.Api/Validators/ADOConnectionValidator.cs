using System.ComponentModel.DataAnnotations;
using Testurio.Api.DTOs;
using Testurio.Core.Enums;

namespace Testurio.Api.Validators;

/// <summary>
/// Validates a <see cref="SaveADOConnectionRequest"/> beyond DataAnnotation constraints.
/// Checks that auth-method-specific credential fields are present.
/// </summary>
public static class ADOConnectionValidator
{
    public static IEnumerable<ValidationResult> Validate(SaveADOConnectionRequest request)
    {
        if (!Uri.TryCreate(request.OrgUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            yield return new ValidationResult(
                "Organization URL must be a valid URL starting with http:// or https://.",
                [nameof(request.OrgUrl)]);
        }

        if (request.AuthMethod == ADOAuthMethod.Pat && string.IsNullOrWhiteSpace(request.Pat))
        {
            yield return new ValidationResult(
                "Personal Access Token is required when PAT auth method is selected.",
                [nameof(request.Pat)]);
        }

        if (request.AuthMethod == ADOAuthMethod.OAuth && string.IsNullOrWhiteSpace(request.OAuthToken))
        {
            yield return new ValidationResult(
                "OAuth token is required when OAuth auth method is selected.",
                [nameof(request.OAuthToken)]);
        }
    }
}
