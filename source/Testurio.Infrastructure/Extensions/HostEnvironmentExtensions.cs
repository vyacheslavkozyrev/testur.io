using Microsoft.Extensions.Hosting;

namespace Testurio.Infrastructure.Extensions;

public static class HostEnvironmentExtensions
{
    /// <summary>
    /// Returns true only for the <c>Test</c> environment (unit &amp; integration test runner).
    /// All other environments — <c>Development</c>, <c>Develop</c>, <c>Production</c> — use Key Vault.
    /// </summary>
    public static bool IsTest(this IHostEnvironment environment) =>
        environment.IsEnvironment("Test");
}
