using System.Text.Json.Serialization;

namespace Testurio.Core.Models;

public class ADOWebhookPayload
{
    [JsonPropertyName("eventType")]
    public string EventType { get; init; } = string.Empty;

    [JsonPropertyName("resource")]
    public ADOWebhookResource? Resource { get; init; }
}

public class ADOWebhookResource
{
    [JsonPropertyName("workItemId")]
    public int WorkItemId { get; init; }

    [JsonPropertyName("fields")]
    public ADOFieldChanges? Fields { get; init; }

    [JsonPropertyName("revision")]
    public ADORevision? Revision { get; init; }
}

public class ADOFieldChanges
{
    [JsonPropertyName("System.State")]
    public ADOFieldChange? State { get; init; }

    [JsonPropertyName("System.WorkItemType")]
    public ADOFieldChange? WorkItemType { get; init; }
}

public class ADOFieldChange
{
    [JsonPropertyName("oldValue")]
    public string? OldValue { get; init; }

    [JsonPropertyName("newValue")]
    public string? NewValue { get; init; }
}

public class ADORevision
{
    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("fields")]
    public ADORevisionFields? Fields { get; init; }
}

public class ADORevisionFields
{
    [JsonPropertyName("System.WorkItemType")]
    public string? WorkItemType { get; init; }

    [JsonPropertyName("System.State")]
    public string? State { get; init; }

    [JsonPropertyName("System.Title")]
    public string? Title { get; init; }

    [JsonPropertyName("System.Description")]
    public string? Description { get; init; }
}
