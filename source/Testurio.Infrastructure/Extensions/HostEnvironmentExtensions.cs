using Microsoft.Extensions.Hosting;

namespace Testurio.Infrastructure.Extensions;

public static class HostEnvironmentExtensions
{
    /// <summary>
    /// Returns true for environments that use local config / user secrets instead of Key Vault:
    /// <c>Development</c> (local dev) and <c>Test</c> (unit &amp; integration test runner).
    /// All other environments — <c>Develop</c>, <c>Production</c>, etc. — require Key Vault.
    /// </summary>
    public static bool IsLocalOrTest(this IHostEnvironment environment) =>
        environment.IsDevelopment() || environment.IsEnvironment("Test");
}
