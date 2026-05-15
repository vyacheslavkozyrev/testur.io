using System.ComponentModel.DataAnnotations;
using Testurio.Api.DTOs;

namespace Testurio.Api.Validators;

/// <summary>
/// Validates the report attachment configuration for a project.
/// Enforces AC-026: reportIncludeScreenshots must not be true when test_type is api.
/// </summary>
public static class ReportConfigurationValidator
{
    /// <summary>
    /// Returns validation errors for <paramref name="request"/>.
    /// <paramref name="testType"/> should be one of: "api", "ui_e2e", "both".
    /// </summary>
    public static IEnumerable<ValidationResult> Validate(UpdateReportSettingsRequest request, string? testType)
    {
        if (request.ReportIncludeScreenshots && IsApiOnly(testType))
        {
            yield return new ValidationResult(
                "reportIncludeScreenshots cannot be true when test_type is api.",
                [nameof(request.ReportIncludeScreenshots)]);
        }
    }

    /// <summary>
    /// Returns true when <paramref name="testType"/> represents an API-only project
    /// for which screenshots are not available (AC-023, AC-026).
    /// </summary>
    public static bool IsApiOnly(string? testType) =>
        string.Equals(testType, "api", StringComparison.OrdinalIgnoreCase);
}
