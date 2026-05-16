namespace Testurio.Core.Models;

/// <summary>
/// Discriminated union representing the resolved access credentials for a project's staging environment.
/// Obtained at pipeline runtime via <see cref="Interfaces.IProjectAccessCredentialProvider"/>.
/// Credential values are never stored beyond a single pipeline run.
/// </summary>
public abstract record ProjectAccessCredentials
{
    private ProjectAccessCredentials() { }

    /// <summary>No credentials — executor connects without authentication.</summary>
    public sealed record IpAllowlist : ProjectAccessCredentials;

    /// <summary>HTTP Basic Auth — username and password resolved from Key Vault.</summary>
    public sealed record BasicAuth(string Username, string Password) : ProjectAccessCredentials;

    /// <summary>Custom HTTP header — header name and resolved value from Key Vault.</summary>
    public sealed record HeaderToken(string HeaderName, string HeaderValue) : ProjectAccessCredentials;
}
