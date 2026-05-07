using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Testurio.Core.Entities;
using Testurio.Core.Models;

namespace Testurio.Plugins.TestGeneratorPlugin;

public partial class TestGeneratorPlugin
{
    private const string SystemPrompt =
        """
        You are a QA engineer specialising in API test case design.
        Given a user story description and acceptance criteria, generate a structured list of API test scenarios.
        Each scenario must test a single behaviour and must NOT include any browser or UI steps.

        Respond ONLY with a valid JSON array. Do not include any explanation, markdown, or prose outside the JSON.
        Each element of the array must have exactly these fields:
        - "title": string — short, descriptive name for the scenario
        - "steps": array of objects, each with:
            - "order": integer starting at 1
            - "description": string — the action to perform (e.g. "Send POST /api/orders with valid payload")
            - "expectedResult": string — what the response or system state should be
        """;

    private readonly IChatCompletionService _chatCompletion;
    private readonly ILogger<TestGeneratorPlugin> _logger;

    public TestGeneratorPlugin(IChatCompletionService chatCompletion, ILogger<TestGeneratorPlugin> logger)
    {
        _chatCompletion = chatCompletion;
        _logger = logger;
    }

    public async Task<IReadOnlyList<TestScenario>> GenerateAsync(
        string testRunId,
        string projectId,
        string storyInput,
        CancellationToken cancellationToken = default)
    {
        var history = new ChatHistory();
        history.AddSystemMessage(SystemPrompt);
        history.AddUserMessage(storyInput);

        var result = await _chatCompletion.GetChatMessageContentsAsync(history, cancellationToken: cancellationToken);
        var responseText = string.Concat(result.Select(r => r.Content));

        if (string.IsNullOrWhiteSpace(responseText))
        {
            LogEmptyResponse(_logger, testRunId);
            return [];
        }

        List<GeneratedScenarioDto>? dtos;
        try
        {
            dtos = JsonSerializer.Deserialize<List<GeneratedScenarioDto>>(responseText, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
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
            Title = dto.Title,
            Steps = dto.Steps
                .OrderBy(s => s.Order)
                .Select(s => new TestScenarioStep
                {
                    Order = s.Order,
                    Description = s.Description,
                    ExpectedResult = s.ExpectedResult
                })
                .ToList()
                .AsReadOnly()
        }).ToList().AsReadOnly();

        LogGenerated(_logger, scenarios.Count, testRunId);
        return scenarios;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Claude returned an empty scenario list for test run {TestRunId}")]
    private static partial void LogEmptyResponse(ILogger logger, string testRunId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to deserialize Claude response for test run {TestRunId}")]
    private static partial void LogDeserializationFailed(ILogger logger, string testRunId, Exception ex);

    [LoggerMessage(Level = LogLevel.Information, Message = "Generated {Count} test scenarios for test run {TestRunId}")]
    private static partial void LogGenerated(ILogger logger, int count, string testRunId);

    private sealed class GeneratedScenarioDto
    {
        public string Title { get; init; } = string.Empty;
        public List<GeneratedStepDto> Steps { get; init; } = [];
    }

    private sealed class GeneratedStepDto
    {
        public int Order { get; init; }
        public string Description { get; init; } = string.Empty;
        public string ExpectedResult { get; init; } = string.Empty;
    }
}
