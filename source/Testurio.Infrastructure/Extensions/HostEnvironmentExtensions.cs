using Microsoft.Extensions.Hosting;

namespace Testurio.Infrastructure.Extensions;

public static class HostEnvironmentExtensions
{
    /// <summary>
    /// Returns true only for the <c>Test</c> environment (unit &amp; integration test runner).
    /// All other environments — <c>Development</c> (local), <c>Develop</c> (staging), <c>Production</c> —
    /// require Azure Key Vault and use <see cref="KeyVault.KeyVaultSecretLoader"/>.
    /// Use this method as the sole gate for NullLoader / DevAuth bypasses; never check for
    /// <c>IsDevelopment()</c> to skip Key Vault, as that was the old <c>IsLocalOrTest()</c> behaviour.
    /// </summary>
    public static bool IsTest(this IHostEnvironment environment) =>
        environment.IsEnvironment("Test");
}
