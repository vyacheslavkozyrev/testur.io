using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Testurio.Core.Exceptions;
using Testurio.Core.Enums;
using Testurio.Core.Interfaces;
using Testurio.Core.Models;
using Testurio.Pipeline.Generators.Services;

namespace Testurio.Pipeline.Generators;

/// <summary>
/// Generator agent that produces typed API test scenarios by calling Claude.
/// Registered in DI with the keyed service key <c>"api"</c> (feature 0028).
/// Implements the retry-on-parse-failure contract described in US-006:
/// up to 4 total attempts; throws <see cref="TestGeneratorException"/> after exhausting all retries.
/// Returns a <see cref="GeneratorResults"/> with <see cref="GeneratorResults.ApiScenarios"/> populated
/// and <see cref="GeneratorResults.UiE2eScenarios"/> always empty.
/// </summary>
public sealed partial class ApiTestGeneratorAgent : ITestGeneratorAgent
{
    private const int MaxAttempts = 4;
    private const int MaxLastResponseLength = 2000;
    private const string CorrectionInstruction =
        "The previous response was not valid JSON. Return only a valid JSON array matching the required schema.";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly ILlmGenerationClient _llmClient;
    private readonly PromptAssemblyService _promptAssembly;
    private readonly ILogger<ApiTestGeneratorAgent> _logger;

    public ApiTestGeneratorAgent(
        ILlmGenerationClient llmClient,
        PromptAssemblyService promptAssembly,
        ILogger<ApiTestGeneratorAgent> logger)
    {
        _llmClient = llmClient;
        _promptAssembly = promptAssembly;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<GeneratorResults> GenerateAsync(GeneratorContext context, CancellationToken ct)
    {
        var userPrompt = _promptAssembly.Assemble(context, out var systemPrompt);

        // Build the message history for the retry loop.
        // The retry appends the previous raw response and a correction instruction before each retry.
        var messages = new List<(string Role, string Content)>
        {
            ("user", userPrompt)
        };

        string lastRawResponse = string.Empty;
        JsonException? lastException = null;

        for (int attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            // Build the combined user message from accumulated history.
            var combinedUser = BuildCombinedMessage(messages);
            lastRawResponse = await _llmClient.CompleteAsync(systemPrompt, combinedUser, ct);

            try
            {
                var scenarios = ParseScenarios(lastRawResponse);

                return new GeneratorResults
                {
                    ApiScenarios = scenarios,
                    UiE2eScenarios = Array.Empty<UiE2eTestScenario>()
                };
            }
            catch (JsonException ex)
            {
                lastException = ex;
                LogRetryAttempt(_logger, context.TestRunId, TestType.Api, attempt, MaxAttempts, ex.Message);

                if (attempt < MaxAttempts)
                {
                    // Append the raw (invalid) response and correction instruction for next attempt.
                    messages.Add(("assistant", lastRawResponse));
                    messages.Add(("user", CorrectionInstruction));
                }
            }
        }

        // All attempts exhausted.
        var truncated = lastRawResponse.Length > MaxLastResponseLength
            ? lastRawResponse[..MaxLastResponseLength]
            : lastRawResponse;

        throw new TestGeneratorException(TestType.Api, MaxAttempts, truncated, lastException!);
    }

    /// <summary>
    /// Parses the raw Claude response into a typed list of <see cref="ApiTestScenario"/>.
    /// Strips markdown code fences if present before parsing.
    /// </summary>
    private static IReadOnlyList<ApiTestScenario> ParseScenarios(string raw)
    {
        var json = StripMarkdownFences(raw.Trim());
        var scenarios = JsonSerializer.Deserialize<List<ApiTestScenario>>(json, JsonOptions);
        if (scenarios is null)
            throw new JsonException("Deserialised scenario list was null.");
        return scenarios;
    }

    /// <summary>
    /// Strips leading <c>```json</c> / <c>```</c> and trailing <c>```</c> markdown fences.
    /// Returns the input unchanged when no fences are found.
    /// </summary>
    private static string StripMarkdownFences(string input)
    {
        if (input.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
            input = input["```json".Length..].TrimStart();
        else if (input.StartsWith("```", StringComparison.Ordinal))
            input = input["```".Length..].TrimStart();

        if (input.EndsWith("```", StringComparison.Ordinal))
            input = input[..^"```".Length].TrimEnd();

        return input;
    }

    /// <summary>
    /// Concatenates all message history entries into a single string.
    /// Role labels are included so Claude can understand the conversation structure
    /// even when sent as a single user message.
    /// </summary>
    private static string BuildCombinedMessage(List<(string Role, string Content)> messages)
    {
        if (messages.Count == 1)
            return messages[0].Content;

        var sb = new StringBuilder();
        foreach (var (role, content) in messages)
        {
            sb.AppendLine($"[{role.ToUpperInvariant()}]");
            sb.AppendLine(content);
            sb.AppendLine();
        }
        return sb.ToString();
    }

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "ApiTestGeneratorAgent: run {TestRunId} — JSON parse failure on attempt {Attempt}/{MaxAttempts} for type {TestType}. Error: {Error}")]
    private static partial void LogRetryAttempt(
        ILogger logger,
        Guid testRunId,
        TestType testType,
        int attempt,
        int maxAttempts,
        string error);
}
