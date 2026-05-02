namespace Testurio.Core.Entities;

public class Project
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public required string UserId { get; init; }
    public required string Name { get; init; }
    public required string ProductUrl { get; init; }
    public required string JiraBaseUrl { get; init; }
    public required string JiraProjectKey { get; init; }
    public required string JiraEmail { get; init; }
    public required string JiraApiToken { get; init; }
    public required string JiraWebhookSecret { get; init; }
    public required string InTestingStatusLabel { get; init; }
}
