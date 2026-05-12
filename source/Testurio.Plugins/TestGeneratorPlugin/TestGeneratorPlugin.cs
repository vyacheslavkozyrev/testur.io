using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Testurio.Core.Entities;
using Testurio.Core.Interfaces;
using Testurio.Core.Models;

namespace Testurio.Plugins.TestGeneratorPlugin;

public partial class TestGeneratorPlugin
{
    /// <summary>
    /// Approximate character-to-token ratio used for the context-limit guard.
    /// Claude's tokeniser averages ~4 characters per token; we use a conservative ratio of 3
    /// so the guard fires before the API rejects the request.
    /// </summary>
    private const int CharsPerToken = 3;

    /// <summary>
    /// Maximum tokens allowed in the composed system prompt (system + strategy + custom).
    /// Claude claude-opus-4-7 supports a 200 000-token context window; we reserve 16 000 for
    /// the generated output and use the remainder for input. The guard is deliberately conservative.
    /// </summary>
    private const int MaxSystemPromptTokens = 4000;

    private const string BaseSystemPrompt =
        """
        You are a QA engineer specialising in API test case design.
        Given a user story description and acceptance criteria, generate a structured list of API test scenarios.
        Each scenario must test a single behaviour and must NOT include any browser or UI steps.

        Respond ONLY with a valid JSON array. Do not include any explanation, markdown, or prose outside the JSON.
        Each element of the array must have exactly these fields:
        - "title": string — short, descriptive name for the scenario
        - "steps": array of objects, each with:
            - "title": string — short name for the step (e.g. "Send valid POST request")
            - "method": string — HTTP method in uppercase (GET, POST, PUT, PATCH, DELETE)
            - "path": string — URL path starting with / (e.g. "/api/orders")
            - "requestBody": string or null — JSON request body if applicable
            - "expectedStatusCode": integer — expected HTTP status code (e.g. 200, 201, 400)
            - "expectedResponseSchema": string or null — JSON object describing expected response fields and types
        """;

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly ILlmGenerationClient _llmClient;
    private readonly ILogger<TestGeneratorPlugin> _logger;

    public TestGeneratorPlugin(ILlmGenerationClient llmClient, ILogger<TestGeneratorPlugin> logger)
    {
        _llmClient = llmClient;
        _logger = logger;
    }

    public async Task<IReadOnlyList<TestScenario>> GenerateAsync(
        string testRunId,
        string projectId,
        string userId,
        string storyInput,
        string testingStrategy,
        string? customPrompt = null,
        CancellationToken cancellationToken = default)
    {
        var composedPrompt = ComposeSystemPrompt(testingStrategy, customPrompt);

        // AC-034: guard against exceeding context limits.
        var estimatedTokens = composedPrompt.Length / CharsPerToken;
        if (estimatedTokens > MaxSystemPromptTokens)
        {
            LogPromptTooLong(_logger, testRunId, estimatedTokens, MaxSystemPromptTokens);
            throw new PromptTooLongException(testRunId,
                $"Composed system prompt is too long ({estimatedTokens} estimated tokens, limit {MaxSystemPromptTokens}). " +
                "Shorten the testing strategy or custom prompt and retry.");
        }

        var responseText = await _llmClient.CompleteAsync(composedPrompt, storyInput, cancellationToken);
        responseText = StripMarkdownFences(responseText);

        if (string.IsNullOrWhiteSpace(responseText))
        {
            LogEmptyResponse(_logger, testRunId);
            return [];
        }

        List<GeneratedScenarioDto>? dtos;
        try
        {
            dtos = JsonSerializer.Deserialize<List<GeneratedScenarioDto>>(responseText, JsonOptions);
        }
        catch (JsonException ex)
        {
            LogDeserializationFailed(_logger, testRunId, ex);
            return [];
        }

        if (dtos is null || dtos.Count == 0)
        {
            LogEmptyResponse(_logger, testRunId);
            return [];
        }

        var scenarios = dtos.Select(dto => new TestScenario
        {
            TestRunId = testRunId,
            ProjectId = projectId,
            UserId = userId,
            Title = dto.Title,
            Steps = dto.Steps
                .Select(s => new TestScenarioStep
                {
                    Title = s.Title,
                    Method = s.Method.ToUpperInvariant(),
                    Path = s.Path.StartsWith('/') ? s.Path : "/" + s.Path,
                    RequestBody = s.RequestBody,
                    ExpectedStatusCode = s.ExpectedStatusCode,
                    ExpectedResponseSchema = s.ExpectedResponseSchema
                })
                .ToList()
                .AsReadOnly()
        }).ToList().AsReadOnly();

        LogGenerated(_logger, scenarios.Count, testRunId);
        return scenarios;
    }

    /// <summary>
    /// Composes the final system prompt: base system prompt → testing strategy → custom prompt.
    /// The order is fixed and cannot be changed (AC-032).
    /// </summary>
    internal static string ComposeSystemPrompt(string testingStrategy, string? customPrompt)
    {
        var sb = new StringBuilder(BaseSystemPrompt);

        if (!string.IsNullOrWhiteSpace(testingStrategy))
        {
            sb.AppendLine();
            sb.AppendLine();
            sb.AppendLine("Testing Strategy:");
            sb.Append(testingStrategy.Trim());
        }

        if (!string.IsNullOrWhiteSpace(customPrompt))
        {
            sb.AppendLine();
            sb.AppendLine();
            sb.AppendLine("Additional Instructions:");
            sb.Append(customPrompt.Trim());
        }

        return sb.ToString();
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

    [LoggerMessage(Level = LogLevel.Warning, Message = "Claude returned an empty scenario list for test run {TestRunId}")]
    private static partial void LogEmptyResponse(ILogger logger, string testRunId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to deserialize Claude response for test run {TestRunId}")]
    private static partial void LogDeserializationFailed(ILogger logger, string testRunId, Exception ex);

    [LoggerMessage(Level = LogLevel.Information, Message = "Generated {Count} test scenarios for test run {TestRunId}")]
    private static partial void LogGenerated(ILogger logger, int count, string testRunId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Composed system prompt for test run {TestRunId} exceeds context limit: {EstimatedTokens} estimated tokens (limit {MaxTokens})")]
    private static partial void LogPromptTooLong(ILogger logger, string testRunId, int estimatedTokens, int maxTokens);

    private sealed class GeneratedScenarioDto
    {
        public string Title { get; init; } = string.Empty;
        public List<GeneratedStepDto> Steps { get; init; } = [];
    }

    private sealed class GeneratedStepDto
    {
        public string Title { get; init; } = string.Empty;
        public string Method { get; init; } = string.Empty;
        public string Path { get; init; } = string.Empty;
        public string? RequestBody { get; init; }
        public int ExpectedStatusCode { get; init; }
        public string? ExpectedResponseSchema { get; init; }
    }
}

/// <summary>
/// Thrown when the composed system prompt exceeds the configured context token limit.
/// </summary>
public class PromptTooLongException : Exception
{
    public string TestRunId { get; }

    public PromptTooLongException(string testRunId, string message)
        : base(message)
    {
        TestRunId = testRunId;
    }
}
