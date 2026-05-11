using System.Text.Json.Serialization;

namespace Testurio.Infrastructure.ADO.Models;

internal sealed class ADOUserResponse
{
    [JsonPropertyName("authenticatedUser")]
    public ADOUserIdentity? AuthenticatedUser { get; init; }
}

internal sealed class ADOUserIdentity
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("providerDisplayName")]
    public string? DisplayName { get; init; }
}
