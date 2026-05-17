namespace Testurio.Core.Constants;

/// <summary>
/// Domain-level constants for the <c>Project</c> entity.
/// Defined once here so that the API validation attributes, service defaults,
/// and pipeline executors all reference a single source of truth.
/// </summary>
public static class ProjectConstants
{
    /// <summary>Minimum allowed value for <c>RequestTimeoutSeconds</c> (inclusive).</summary>
    public const int RequestTimeoutMinSeconds = 5;

    /// <summary>Maximum allowed value for <c>RequestTimeoutSeconds</c> (inclusive).</summary>
    public const int RequestTimeoutMaxSeconds = 120;

    /// <summary>Default value written to new projects when <c>RequestTimeoutSeconds</c> is not explicitly supplied.</summary>
    public const int RequestTimeoutDefaultSeconds = 30;
}
