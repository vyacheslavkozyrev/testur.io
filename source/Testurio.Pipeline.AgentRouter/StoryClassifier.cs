using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Testurio.Core.Enums;
using Testurio.Core.Interfaces;
using Testurio.Core.Models;

namespace Testurio.Pipeline.AgentRouter;

/// <summary>
/// Calls the Claude API to classify a <see cref="ParsedStory"/> into the applicable test types
/// (<see cref="TestType.Api"/> and/or <see cref="TestType.UiE2e"/>).
/// Returns the suggested types and a brief rationale string.
/// This class performs the Claude call and JSON parsing only; project-config filtering is done
/// by the caller (<see cref="AgentRouterService"/>).
/// <para>
/// Feature 0047: the system prompt is loaded from the <c>PromptTemplates</c> Cosmos container via
/// <see cref="IPromptTemplateService"/> at the start of each <see cref="ClassifyAsync"/> invocation,
/// replacing the previous hardcoded <c>const string SystemPrompt</c>.
/// </para>
/// </summary>
public sealed partial class StoryClassifier
{
    private static readonly JsonSerializerOptions JsonOptions;

    static StoryClassifier()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            TypeInfoResolver = new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver()
        };
        options.MakeReadOnly();
        JsonOptions = options;
    }

    private readonly ILlmGenerationClient _llmClient;
    private readonly IPromptTemplateService _promptTemplateService;
    private readonly ILogger<StoryClassifier> _logger;

    public StoryClassifier(
        ILlmGenerationClient llmClient,
        IPromptTemplateService promptTemplateService,
        ILogger<StoryClassifier> logger)
    {
        _llmClient = llmClient;
        _promptTemplateService = promptTemplateService;
        _logger = logger;
    }

    /// <summary>
    /// Classifies the <paramref name="parsedStory"/> using Claude.
    /// Returns the suggested <see cref="TestType"/> values and classification reason.
    /// Does NOT apply project-config filtering — the caller must filter the result.
    /// </summary>
    /// <exception cref="HttpRequestException">Propagated when the Claude API call fails.</exception>
    /// <exception cref="InvalidOperationException">Thrown when Claude returns a malformed or unrecognisable JSON response.</exception>
    public async Task<(TestType[] SuggestedTypes, string Reason)> ClassifyAsync(
        ParsedStory parsedStory,
        CancellationToken ct = default)
    {
        var systemPrompt = await _promptTemplateService.GetActiveBodyAsync("agent_router", ct);

        var userMessage = BuildUserMessage(parsedStory);

        LogClassifying(_logger, parsedStory.Title);

        string rawResponse = await _llmClient.CompleteAsync(systemPrompt, userMessage, ct);

        return ParseClassificationResponse(rawResponse);
    }

    private static string BuildUserMessage(ParsedStory parsedStory)
    {
        var acLines = string.Join("\n", parsedStory.AcceptanceCriteria.Select((ac, i) => $"{i + 1}. {ac}"));
        var entitiesLine = parsedStory.Entities.Count > 0
            ? string.Join(", ", parsedStory.Entities)
            : "(none detected)";
        var actionsLine = parsedStory.Actions.Count > 0
            ? string.Join(", ", parsedStory.Actions)
            : "(none detected)";

        return
            $"""
            Title: {parsedStory.Title}

            Description:
            {parsedStory.Description}

            Acceptance Criteria:
            {acLines}

            Entities: {entitiesLine}
            Actions: {actionsLine}
            """;
    }

    private static (TestType[] SuggestedTypes, string Reason) ParseClassificationResponse(string rawResponse)
    {
        var trimmed = rawResponse.Trim();

        // Strip code fences if Claude ignores the system prompt instruction.
        if (trimmed.StartsWith("```"))
        {
            var firstNewline = trimmed.IndexOf('\n');
            var lastFence = trimmed.LastIndexOf("```", StringComparison.Ordinal);
            if (firstNewline > 0 && lastFence > firstNewline)
                trimmed = trimmed[(firstNewline + 1)..lastFence].Trim();
        }

        ClassificationResponseDto dto;
        try
        {
            dto = JsonSerializer.Deserialize<ClassificationResponseDto>(trimmed, JsonOptions)
                  ?? throw new InvalidOperationException("AgentRouter: classification response deserialised to null");
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"AgentRouter: failed to parse classification response — {ex.Message}", ex);
        }

        if (string.IsNullOrWhiteSpace(dto.Reason))
            throw new InvalidOperationException("AgentRouter: classification response missing 'reason' field");

        // Null test_types array is treated as an empty array (no applicable test type).
        var rawTypes = dto.TestTypes ?? Array.Empty<string>();
        var resolved = new List<TestType>(rawTypes.Length);
        foreach (var raw in rawTypes)
        {
            if (string.Equals(raw, "api", StringComparison.OrdinalIgnoreCase))
                resolved.Add(TestType.Api);
            else if (string.Equals(raw, "ui_e2e", StringComparison.OrdinalIgnoreCase))
                resolved.Add(TestType.UiE2e);
            // Unknown values are silently ignored — the classifier only surfaces MVP types.
        }

        return (resolved.ToArray(), dto.Reason.Trim());
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "StoryClassifier: classifying story '{Title}'")]
    private static partial void LogClassifying(ILogger logger, string title);

    /// <summary>Internal DTO for JSON deserialisation of the Claude classification response.</summary>
    private sealed class ClassificationResponseDto
    {
        [JsonPropertyName("test_types")]
        public string[]? TestTypes { get; init; }

        [JsonPropertyName("reason")]
        public string? Reason { get; init; }
    }
}
