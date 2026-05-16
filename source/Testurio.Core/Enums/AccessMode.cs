namespace Testurio.Core.Enums;

/// <summary>
/// Determines how the test executor authenticates against the client's staging environment.
/// </summary>
public enum AccessMode
{
    /// <summary>No credentials — client adds Testurio's published egress IPs to their firewall.</summary>
    IpAllowlist,

    /// <summary>HTTP Basic Auth — username and password stored in Key Vault.</summary>
    BasicAuth,

    /// <summary>Custom HTTP header token — header name in Cosmos, value in Key Vault.</summary>
    HeaderToken,
}
