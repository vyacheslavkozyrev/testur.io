namespace Testurio.Infrastructure.KeyVault;

/// <summary>
/// Provides Key Vault naming conventions for project-scoped secrets.
/// At project creation time a logical namespace is established using the projectId
/// as a path segment so future secret writes (Jira tokens, access credentials) are
/// consistently addressable without needing to know the naming convention at call sites.
/// No Azure SDK call is made at creation time — Key Vault secrets are created on demand
/// when the respective feature writes its first secret.
/// </summary>
public static class ProjectSecretNamespace
{
    /// <summary>
    /// Returns the Key Vault secret name for a given project and secret key.
    /// Format: <c>projects--{projectId}--{secretKey}</c>
    /// (double dash is the Key Vault-safe path separator, since slashes are not allowed in secret names).
    /// </summary>
    public static string SecretName(string projectId, string secretKey)
        => $"projects--{projectId}--{secretKey}";

    /// <summary>
    /// Returns the Key Vault secret name prefix for all secrets belonging to a project.
    /// Use this when listing or purging all secrets for a project.
    /// </summary>
    public static string NamespacePrefix(string projectId)
        => $"projects--{projectId}--";

    // Access credential secret key constants (feature 0017)
    public const string BasicAuthUser = "basic-auth-user";
    public const string BasicAuthPass = "basic-auth-pass";
    public const string HeaderTokenValue = "header-token-value";
}
