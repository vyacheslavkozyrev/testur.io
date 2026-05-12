using System.ComponentModel.DataAnnotations;
using Testurio.Core.Enums;

namespace Testurio.Api.DTOs;

/// <summary>
/// Request body for saving a Jira PM tool connection.
/// POST /v1/projects/{projectId}/integrations/jira
/// </summary>
public sealed record SaveJiraConnectionRequest(
    [property: Required]
    [property: MaxLength(500)]
    string BaseUrl,

    [property: Required]
    [property: MaxLength(200)]
    string ProjectKey,

    [property: Required]
    [property: MaxLength(200)]
    string InTestingStatus,

    [property: Required]
    JiraAuthMethod AuthMethod,

    // Auth-method-specific credentials — validated conditionally by JiraConnectionValidator
    string? Email,
    string? ApiToken,
    string? Pat);
