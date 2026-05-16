using Testurio.Core.Enums;

namespace Testurio.Core.Entities;

public class Project
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public required string UserId { get; init; }
    public required string Name { get; set; }
    public required string ProductUrl { get; set; }
    public required string TestingStrategy { get; set; }

    /// <summary>
    /// Structured test types enabled for this project (MVP: api, ui_e2e, or both).
    /// Null or empty defaults to all MVP types (<see cref="TestType.Api"/> and <see cref="TestType.UiE2e"/>)
    /// for backwards compatibility with projects created before feature 0026.
    /// </summary>
    public TestType[]? TestTypes { get; set; }

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

    // Work item type filter — feature 0020
    /// <summary>
    /// Issue type names that are allowed to trigger a test run. Null means use the PM-tool default list.
    /// Jira default: ["Story", "Bug"]. ADO default: ["User Story", "Bug"].
    /// </summary>
    public string[]? AllowedWorkItemTypes { get; set; }

    /// <summary>Returns the default allowed issue types for the given PM tool.</summary>
    public static string[] GetDefaultAllowedWorkItemTypes(PMToolType pmTool) => pmTool switch
    {
        PMToolType.Ado  => ["User Story", "Bug"],
        PMToolType.Jira => ["Story", "Bug"],
        _               => ["Story", "Bug"],
    };

    // Report format & attachment settings — feature 0009
    /// <summary>Blob Storage URI of the custom report template (.md file). Null means built-in default is used.</summary>
    public string? ReportTemplateUri { get; set; }
    /// <summary>When true, step-by-step execution logs are included in the rendered report. Default: true.</summary>
    public bool ReportIncludeLogs { get; set; } = true;
    /// <summary>When true, screenshot attachments are included in the rendered report. Default: true. Only meaningful when test_type is ui_e2e or both.</summary>
    public bool ReportIncludeScreenshots { get; set; } = true;

    // Legacy fields — kept for backward compatibility until migration removes them
    public string? JiraEmail { get; set; }
    public string? JiraApiTokenSecretRef { get; set; }
    public string? JiraWebhookSecretRef { get; set; }
    public string? InTestingStatusLabel { get; set; }
    public string? BearerTokenSecretRef { get; set; }
}
