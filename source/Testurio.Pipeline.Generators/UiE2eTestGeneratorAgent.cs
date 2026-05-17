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
/// Generator agent that produces typed UI end-to-end test scenarios by calling Claude.
/// Registered in DI with the keyed service key <c>"ui_e2e"</c> (feature 0028).
/// Implements the retry-on-parse-failure contract described in US-006:
/// up to 4 total attempts; throws <see cref="TestGeneratorException"/> after exhausting all retries.
/// Returns a <see cref="GeneratorResults"/> with <see cref="GeneratorResults.UiE2eScenarios"/> populated
/// and <see cref="GeneratorResults.ApiScenarios"/> always empty.
/// The generator instruction enforces selector preference order and the assertion-step requirement
/// (US-004 AC-021, AC-022).
/// </summary>
public sealed partial class UiE2eTestGeneratorAgent : ITestGeneratorAgent
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
    private readonly ILogger<UiE2eTestGeneratorAgent> _logger;

    public UiE2eTestGeneratorAgent(
        ILlmGenerationClient llmClient,
        PromptAssemblyService promptAssembly,
        ILogger<UiE2eTestGeneratorAgent> logger)
    {
        _llmClient = llmClient;
        _promptAssembly = promptAssembly;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<GeneratorResults> GenerateAsync(GeneratorContext context, CancellationToken ct)
    {
        var userPrompt = _promptAssembly.Assemble(context, out var systemPrompt);

        var messages = new List<(string Role, string Content)>
        {
            ("user", userPrompt)
        };

        string lastRawResponse = string.Empty;
        JsonException? lastException = null;

        for (int attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            var combinedUser = BuildCombinedMessage(messages);
            lastRawResponse = await _llmClient.CompleteAsync(systemPrompt, combinedUser, ct);

            try
            {
                var scenarios = ParseScenarios(lastRawResponse);

                return new GeneratorResults
                {
                    ApiScenarios = Array.Empty<ApiTestScenario>(),
                    UiE2eScenarios = scenarios
                };
            }
            catch (JsonException ex)
            {
                lastException = ex;
                LogRetryAttempt(_logger, context.TestRunId, TestType.UiE2e, attempt, MaxAttempts, ex.Message);

                if (attempt < MaxAttempts)
                {
                    messages.Add(("assistant", lastRawResponse));
                    messages.Add(("user", CorrectionInstruction));
                }
            }
        }

        var truncated = lastRawResponse.Length > MaxLastResponseLength
            ? lastRawResponse[..MaxLastResponseLength]
            : lastRawResponse;

        throw new TestGeneratorException(TestType.UiE2e, MaxAttempts, truncated, lastException!);
    }

    /// <summary>
    /// Parses the raw Claude response into a typed list of <see cref="UiE2eTestScenario"/>.
    /// Strips markdown code fences if present before parsing.
    /// </summary>
    private static IReadOnlyList<UiE2eTestScenario> ParseScenarios(string raw)
    {
        var json = StripMarkdownFences(raw.Trim());
        var scenarios = JsonSerializer.Deserialize<List<UiE2eTestScenario>>(json, JsonOptions);
        if (scenarios is null)
            throw new JsonException("Deserialised scenario list was null.");
        return scenarios;
    }

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
        Message = "UiE2eTestGeneratorAgent: run {TestRunId} — JSON parse failure on attempt {Attempt}/{MaxAttempts} for type {TestType}. Error: {Error}")]
    private static partial void LogRetryAttempt(
        ILogger logger,
        Guid testRunId,
        TestType testType,
        int attempt,
        int maxAttempts,
        string error);
}
