using System.Text.Json.Serialization;

namespace Testurio.Infrastructure.Jira.Models;

internal sealed class JiraProjectResponse
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("key")]
    public string? Key { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }
}
