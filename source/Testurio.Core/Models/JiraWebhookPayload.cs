using System.Text.Json.Serialization;

namespace Testurio.Core.Models;

public class JiraWebhookPayload
{
    [JsonPropertyName("webhookEvent")]
    public string WebhookEvent { get; init; } = string.Empty;

    [JsonPropertyName("issue")]
    public JiraIssue? Issue { get; init; }

    [JsonPropertyName("transition")]
    public JiraTransition? Transition { get; init; }

    [JsonPropertyName("changelog")]
    public JiraChangelog? Changelog { get; init; }
}

public class JiraIssue
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("key")]
    public string Key { get; init; } = string.Empty;

    [JsonPropertyName("fields")]
    public JiraIssueFields? Fields { get; init; }
}

public class JiraIssueFields
{
    [JsonPropertyName("summary")]
    public string? Summary { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("issuetype")]
    public JiraIssueType? IssueType { get; init; }

    [JsonPropertyName("status")]
    public JiraStatus? Status { get; init; }
}

public class JiraIssueType
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;
}

public class JiraStatus
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;
}

public class JiraTransition
{
    [JsonPropertyName("to")]
    public JiraTransitionTo? To { get; init; }
}

public class JiraTransitionTo
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;
}

public class JiraChangelog
{
    [JsonPropertyName("items")]
    public List<JiraChangelogItem> Items { get; init; } = [];
}

public class JiraChangelogItem
{
    [JsonPropertyName("field")]
    public string Field { get; init; } = string.Empty;

    [JsonPropertyName("toString")]
    public string? ToString { get; init; }
}
