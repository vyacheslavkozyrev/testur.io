using System.Text.Json;
using Microsoft.Extensions.Logging;
using Testurio.Core.Exceptions;
using Testurio.Core.Interfaces;
using Testurio.Core.Models;

namespace Testurio.Pipeline.StoryParser;

/// <summary>
/// Converts a non-conformant <see cref="WorkItem"/> into a <see cref="ParsedStory"/> by calling
/// the Claude API and deserialising its JSON response against the ParsedStory schema.
/// Throws <see cref="StoryParserException"/> when the response is malformed or missing required fields.
/// </summary>
public sealed partial class AiStoryConverter
{
    private const string SystemPrompt =
        """
        You are a story parsing assistant for an automated software testing platform.

        Given a raw work item (user story or bug report), extract and return ONLY a valid JSON object
        matching this exact schema (no markdown, no explanation, no code fences):

        {
          "title": "<string — non-empty>",
          "description": "<string — non-empty>",
          "acceptance_criteria": ["<string>", ...],
          "entities": ["<string>", ...],
          "actions": ["<string>", ...],
          "edge_cases": ["<string>", ...]
        }

        Rules:
        - title, description, and acceptance_criteria are REQUIRED and must be non-empty.
        - entities, actions, and edge_cases may be empty arrays [] when not applicable.
        - Do NOT include any text outside the JSON object.
        """;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private readonly ILlmGenerationClient _llmClient;
    private readonly ILogger<AiStoryConverter> _logger;

    public AiStoryConverter(ILlmGenerationClient llmClient, ILogger<AiStoryConverter> logger)
    {
        _llmClient = llmClient;
        _logger = logger;
    }

    /// <summary>
    /// Calls Claude to convert the raw <paramref name="workItem"/> content into a structured <see cref="ParsedStory"/>.
    /// </summary>
    /// <exception cref="StoryParserException">
    /// Thrown when the Claude response cannot be parsed or fails schema validation.
    /// </exception>
    public async Task<ParsedStory> ConvertAsync(WorkItem workItem, CancellationToken ct = default)
    {
        var userMessage =
            $"""
            Title: {workItem.Title}

            Description:
            {workItem.Description}

            Acceptance Criteria:
            {workItem.AcceptanceCriteria}
            """;

        string rawResponse;
        try
        {
            rawResponse = await _llmClient.CompleteAsync(SystemPrompt, userMessage, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogClaudeApiError(_logger, ex);
            throw new StoryParserException("StoryParser: Claude API call failed", ex);
        }

        return ParseAndValidate(rawResponse);
    }

    private static ParsedStory ParseAndValidate(string rawResponse)
    {
        AiResponseDto dto;
        try
        {
            // Strip code fences if Claude ignores the system prompt instruction.
            var json = StripCodeFences(rawResponse);
            dto = JsonSerializer.Deserialize<AiResponseDto>(json, JsonOptions)
                  ?? throw new JsonException("Deserialised object was null");
        }
        catch (JsonException ex)
        {
            throw new StoryParserException("StoryParser: invalid AI response", ex);
        }

        if (string.IsNullOrWhiteSpace(dto.Title))
            throw new StoryParserException("StoryParser: invalid AI response — title is missing or empty");

        if (string.IsNullOrWhiteSpace(dto.Description))
            throw new StoryParserException("StoryParser: invalid AI response — description is missing or empty");

        if (dto.AcceptanceCriteria is null || dto.AcceptanceCriteria.Length == 0)
            throw new StoryParserException("StoryParser: invalid AI response — acceptance_criteria is missing or empty");

        return new ParsedStory
        {
            Title = dto.Title.Trim(),
            Description = dto.Description.Trim(),
            AcceptanceCriteria = dto.AcceptanceCriteria
                .Select(s => s?.Trim() ?? string.Empty)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToList()
                .AsReadOnly(),
            Entities = dto.Entities ?? Array.Empty<string>(),
            Actions = dto.Actions ?? Array.Empty<string>(),
            EdgeCases = dto.EdgeCases ?? Array.Empty<string>()
        };
    }

    private static string StripCodeFences(string raw)
    {
        var trimmed = raw.Trim();
        if (trimmed.StartsWith("```"))
        {
            var firstNewline = trimmed.IndexOf('\n');
            var lastFence = trimmed.LastIndexOf("```", StringComparison.Ordinal);
            if (firstNewline > 0 && lastFence > firstNewline)
                return trimmed[(firstNewline + 1)..lastFence].Trim();
        }
        return trimmed;
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "AiStoryConverter: Claude API call failed")]
    private static partial void LogClaudeApiError(ILogger logger, Exception ex);

    /// <summary>Internal DTO for JSON deserialization of the Claude response.</summary>
    private sealed class AiResponseDto
    {
        public string? Title { get; init; }
        public string? Description { get; init; }
        public string[]? AcceptanceCriteria { get; init; }
        public string[]? Entities { get; init; }
        public string[]? Actions { get; init; }
        public string[]? EdgeCases { get; init; }
    }
}
