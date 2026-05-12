using Testurio.Core.Enums;

namespace Testurio.Core.Entities;

public class Project
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public required string UserId { get; init; }
    public required string Name { get; set; }
    public required string ProductUrl { get; set; }
    public required string TestingStrategy { get; set; }
    /// <summary>Optional free-text prompt appended after the testing strategy when calling the TestGenerator. Null means no custom guidance.</summary>
    public string? CustomPrompt { get; set; }
    public bool IsDeleted { get; set; } = false;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    // PM tool integration — populated by feature 0007
    public PMToolType? PmTool { get; set; }
    public IntegrationStatus IntegrationStatus { get; set; } = IntegrationStatus.None;

    // ADO-specific fields
    public string? AdoOrgUrl { get; set; }
    public string? AdoProjectName { get; set; }
    public string? AdoTeam { get; set; }
    public string? AdoInTestingStatus { get; set; }
    public ADOAuthMethod? AdoAuthMethod { get; set; }
    /// <summary>Key Vault secret URI for the ADO PAT or OAuth token. Never the raw value.</summary>
    public string? AdoTokenSecretUri { get; set; }

    // Jira-specific fields
    public string? JiraBaseUrl { get; set; }
    public string? JiraProjectKey { get; set; }
    public string? JiraInTestingStatus { get; set; }
    public JiraAuthMethod? JiraAuthMethod { get; set; }
    /// <summary>Key Vault secret URI for the Jira API token (apiToken auth method). Never the raw value.</summary>
    public string? JiraApiTokenSecretUri { get; set; }
    /// <summary>Key Vault secret URI for the Jira email (apiToken auth method). Never the raw value.</summary>
    public string? JiraEmailSecretUri { get; set; }
    /// <summary>Key Vault secret URI for the Jira PAT (pat auth method). Never the raw value.</summary>
    public string? JiraPatSecretUri { get; set; }

    // Webhook
    /// <summary>Key Vault secret URI for the per-project webhook secret. Never the raw value.</summary>
    public string? WebhookSecretUri { get; set; }
    /// <summary>True after the webhook secret has been viewed once in plaintext; subsequent views are masked.</summary>
    public bool WebhookSecretViewed { get; set; } = false;

    // Legacy fields — kept for backward compatibility until migration removes them
    public string? JiraEmail { get; set; }
    public string? JiraApiTokenSecretRef { get; set; }
    public string? JiraWebhookSecretRef { get; set; }
    public string? InTestingStatusLabel { get; set; }
    public string? BearerTokenSecretRef { get; set; }
}
