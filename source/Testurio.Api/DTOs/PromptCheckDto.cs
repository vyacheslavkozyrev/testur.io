using System.ComponentModel.DataAnnotations;

namespace Testurio.Api.DTOs;

/// <summary>
/// Request body for the prompt quality check endpoint.
/// </summary>
public sealed record PromptCheckRequest(
    [property: Required]
    [property: MinLength(1)]
    [property: MaxLength(5000)]
    string CustomPrompt);

/// <summary>
/// Structured feedback returned by the AI prompt quality check.
/// </summary>
public sealed record PromptCheckFeedback(
    PromptCheckDimension Clarity,
    PromptCheckDimension Specificity,
    PromptCheckDimension PotentialConflicts);

/// <summary>
/// A single feedback dimension with an assessment and an optional improvement suggestion.
/// </summary>
public sealed record PromptCheckDimension(
    string Assessment,
    string? Suggestion);
