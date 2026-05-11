using System.ComponentModel.DataAnnotations;
using Testurio.Core.Enums;

namespace Testurio.Api.DTOs;

/// <summary>
/// Request body for saving an Azure DevOps PM tool connection.
/// POST /v1/projects/{projectId}/integrations/ado
/// </summary>
public sealed record SaveADOConnectionRequest(
    [property: Required]
    [property: MaxLength(500)]
    string OrgUrl,

    [property: Required]
    [property: MaxLength(200)]
    string ProjectName,

    [property: Required]
    [property: MaxLength(200)]
    string Team,

    [property: Required]
    [property: MaxLength(200)]
    string InTestingStatus,

    [property: Required]
    ADOAuthMethod AuthMethod,

    // Auth-method-specific credentials — validated conditionally by ADOConnectionValidator
    string? Pat,
    string? OAuthToken);
