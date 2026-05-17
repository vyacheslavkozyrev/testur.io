using System.ComponentModel.DataAnnotations;
using Testurio.Core.Constants;

namespace Testurio.Api.DTOs;

/// <summary>
/// Response body for project list and detail endpoints.
/// </summary>
public sealed record ProjectDto(
    string ProjectId,
    string Name,
    string ProductUrl,
    string TestingStrategy,
    string? CustomPrompt,
    string[]? AllowedWorkItemTypes,
    int RequestTimeoutSeconds,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <summary>
/// Request body for creating a new project (POST /v1/projects).
/// All three fields are required; validation is enforced by .NET built-in parameter validation.
/// </summary>
public sealed record CreateProjectRequest(
    [property: Required]
    [property: MaxLength(200)]
    string Name,

    [property: Required]
    [property: Url]
    string ProductUrl,

    [property: Required]
    [property: MaxLength(500)]
    string TestingStrategy,

    [property: MaxLength(5000)]
    string? CustomPrompt = null,

    [property: Range(ProjectConstants.RequestTimeoutMinSeconds, ProjectConstants.RequestTimeoutMaxSeconds)]
    int RequestTimeoutSeconds = ProjectConstants.RequestTimeoutDefaultSeconds);

/// <summary>
/// Request body for updating an existing project's core configuration (PUT /v1/projects/{id}).
/// </summary>
public sealed record UpdateProjectRequest(
    [property: Required]
    [property: MaxLength(200)]
    string Name,

    [property: Required]
    [property: Url]
    string ProductUrl,

    [property: Required]
    [property: MaxLength(500)]
    string TestingStrategy,

    [property: MaxLength(5000)]
    string? CustomPrompt = null,

    [property: Range(ProjectConstants.RequestTimeoutMinSeconds, ProjectConstants.RequestTimeoutMaxSeconds)]
    int RequestTimeoutSeconds = ProjectConstants.RequestTimeoutDefaultSeconds);
