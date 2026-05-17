using Testurio.Core.Enums;

namespace Testurio.Core.Models;

/// <summary>
/// Discriminated union representing the resolved API test authentication credentials for a project.
/// Obtained at pipeline runtime via <see cref="Interfaces.IApiTestAuthCredentialProvider"/>.
/// Credential values are never stored beyond a single pipeline run.
/// </summary>
public abstract record ApiTestAuthCredentials
{
    private ApiTestAuthCredentials() { }

    /// <summary>No authentication — requests are sent without any auth header.</summary>
    public sealed record None : ApiTestAuthCredentials;

    /// <summary>Bearer token authentication — injected as <c>Authorization: Bearer {Token}</c>.</summary>
    public sealed record Bearer(string Token) : ApiTestAuthCredentials;

    /// <summary>API key authentication — injected as a header or query parameter.</summary>
    public sealed record ApiKey(string Name, ApiAuthApiKeyPlacement Placement, string Value) : ApiTestAuthCredentials;

    /// <summary>HTTP Basic Auth — encoded as <c>Authorization: Basic {base64(username:password)}</c>.</summary>
    public sealed record Basic(string Username, string Password) : ApiTestAuthCredentials;
}
