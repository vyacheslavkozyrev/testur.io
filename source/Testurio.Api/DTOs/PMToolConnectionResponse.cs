using Testurio.Core.Enums;

namespace Testurio.Api.DTOs;

/// <summary>
/// Integration status and configuration returned by GET /v1/projects/{projectId}/integrations.
/// Never includes raw secret values — only labels, URIs, and status flags.
/// </summary>
public sealed record PMToolConnectionResponse(
    PMToolType? PmTool,
    IntegrationStatus IntegrationStatus,

    // ADO fields
    string? AdoOrgUrl,
    string? AdoProjectName,
    string? AdoTeam,
    string? AdoInTestingStatus,
    ADOAuthMethod? AdoAuthMethod,
    string? AdoTokenSecretUri,

    // Jira fields
    string? JiraBaseUrl,
    string? JiraProjectKey,
    string? JiraInTestingStatus,
    JiraAuthMethod? JiraAuthMethod,
    string? JiraApiTokenSecretUri,
    string? JiraEmailSecretUri,
    string? JiraPatSecretUri);
