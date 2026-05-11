using System.Text.Json.Serialization;

namespace Testurio.Infrastructure.ADO.Models;

internal sealed class ADOProjectResponse
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("state")]
    public string? State { get; init; }
}
