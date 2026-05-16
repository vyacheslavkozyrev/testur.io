namespace Testurio.Core.Enums;

/// <summary>
/// The category of test to generate and execute.
/// MVP supports <see cref="Api"/> and <see cref="UiE2e"/> only.
/// Post-MVP types (smoke, a11y, visual, performance) are explicitly out of scope for v1.
/// </summary>
public enum TestType
{
    /// <summary>HTTP-level API tests — exercises endpoints, status codes, and JSON responses.</summary>
    Api,

    /// <summary>Browser-based end-to-end tests — exercises the UI via Playwright.</summary>
    UiE2e
}
