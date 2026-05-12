using System.Text.Json;
using Microsoft.Extensions.Logging;
using Testurio.Api.DTOs;
using Testurio.Core.Interfaces;

namespace Testurio.Api.Services;

public interface IPromptCheckService
{
    Task<PromptCheckFeedback> CheckAsync(string customPrompt, string testingStrategy, CancellationToken cancellationToken = default);
}

public partial class PromptCheckService : IPromptCheckService
{
    private const string CheckSystemPrompt =
        """
        You are a QA prompt quality reviewer. Your job is to evaluate a custom test generation prompt
        that will be appended to a fixed system prompt and a project testing strategy when generating API test scenarios.

        Respond ONLY with a valid JSON object. Do not include any explanation, markdown, or prose outside the JSON.
        The object must have exactly these fields:
        - "clarity": object with:
            - "assessment": string — short assessment of how clear the prompt is
            - "suggestion": string or null — concrete improvement suggestion if applicable
        - "specificity": object with:
            - "assessment": string — short assessment of how specific the prompt is
            - "suggestion": string or null — concrete improvement suggestion if applicable
        - "potentialConflicts": object with:
            - "assessment": string — short assessment of potential conflicts with the testing strategy
            - "suggestion": string or null — concrete improvement suggestion if applicable
        """;

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly ILlmGenerationClient _llmClient;
    private readonly ILogger<PromptCheckService> _logger;

    public PromptCheckService(ILlmGenerationClient llmClient, ILogger<PromptCheckService> logger)
    {
        _llmClient = llmClient;
        _logger = logger;
    }

    public async Task<PromptCheckFeedback> CheckAsync(
        string customPrompt,
        string testingStrategy,
        CancellationToken cancellationToken = default)
    {
        var userMessage =
            $"""
            Testing Strategy:
            {testingStrategy}

            Custom Prompt to evaluate:
            {customPrompt}
            """;

        var responseText = await _llmClient.CompleteAsync(CheckSystemPrompt, userMessage, cancellationToken);
        responseText = StripMarkdownFences(responseText);

        PromptCheckFeedback? feedback;
        try
        {
            feedback = JsonSerializer.Deserialize<PromptCheckFeedback>(responseText, JsonOptions);
        }
        catch (JsonException ex)
        {
            LogDeserializationFailed(_logger, ex);
            throw new InvalidOperationException("Failed to parse prompt check response from AI.", ex);
        }

        if (feedback is null)
        {
            LogEmptyResponse(_logger);
            throw new InvalidOperationException("AI returned an empty prompt check response.");
        }

        return feedback;
    }

    private static string StripMarkdownFences(string text)
    {
        if (!text.StartsWith("```", StringComparison.Ordinal))
            return text;
        var firstNewline = text.IndexOf('\n');
        if (firstNewline < 0)
            return text;
        var body = text[(firstNewline + 1)..];
        if (body.EndsWith("```", StringComparison.Ordinal))
            body = body[..^3];
        return body.Trim();
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to deserialize prompt check response from AI")]
    private static partial void LogDeserializationFailed(ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "AI returned an empty prompt check response")]
    private static partial void LogEmptyResponse(ILogger logger);
}
