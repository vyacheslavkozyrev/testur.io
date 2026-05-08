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
    public required string JiraApiTokenSecretRef { get; init; }
    public required string JiraWebhookSecretRef { get; init; }
    public required string InTestingStatusLabel { get; init; }

    /// <summary>
    /// Key Vault secret reference for the Bearer token used to authenticate API test requests.
    /// Null when no authentication is required (AC-007).
    /// </summary>
    public string? BearerTokenSecretRef { get; init; }
}
