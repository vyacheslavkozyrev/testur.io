namespace Testurio.Core.Entities;

public class Project
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public required string UserId { get; init; }
    public required string Name { get; set; }
    public required string ProductUrl { get; set; }
    public required string TestingStrategy { get; set; }
    public bool IsDeleted { get; set; } = false;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    // Jira integration fields — populated by feature 0007
    public string? JiraBaseUrl { get; set; }
    public string? JiraProjectKey { get; set; }
    public string? JiraEmail { get; set; }
    public string? JiraApiTokenSecretRef { get; set; }
    public string? JiraWebhookSecretRef { get; set; }
    public string? InTestingStatusLabel { get; set; }
    public string? BearerTokenSecretRef { get; set; }
}
