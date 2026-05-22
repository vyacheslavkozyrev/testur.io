using Testurio.Core.Entities;
using Testurio.Core.Models;
using Testurio.Pipeline.Generators.Services;

namespace Testurio.UnitTests.Pipeline.Generators;

public class PromptAssemblyServiceTests
{
    private readonly PromptAssemblyService _sut = new();

    private static ParsedStory MakeStory(
        string[]? entities = null,
        string[]? actions = null,
        string[]? edgeCases = null) => new()
        {
            Title = "Create order",
            Description = "User creates a new order.",
            AcceptanceCriteria = ["POST /orders returns 201", "Order stored in DB"],
            Entities = entities ?? [],
            Actions = actions ?? [],
            EdgeCases = edgeCases ?? []
        };

    private static MemoryRetrievalResult EmptyMemory() => new()
    {
        Scenarios = []
    };

    private static MemoryRetrievalResult MemoryWithScenarios() => new()
    {
        Scenarios =
        [
            new TestMemoryEntry
            {
                UserId = "user1",
                TestType = "api",
                StoryText = "Old story text",
                ScenarioText = "[{\"id\":\"abc\"}]",
                PassRate = 0.9
            }
        ]
    };

    private static Project MakeProject(string? customPrompt = null) => new()
    {
        UserId = "user1",
        Name = "Test Project",
        ProductUrl = "https://app.example.com",
        TestingStrategy = "Focus on REST endpoints",
        CustomPrompt = customPrompt
    };

    private GeneratorContext MakeContext(
        MemoryRetrievalResult? memory = null,
        string? customPrompt = null,
        string systemPrompt = "You are an expert API test engineer.") => new()
        {
            ParsedStory = MakeStory(),
            MemoryRetrievalResult = memory ?? EmptyMemory(),
            ProjectConfig = MakeProject(customPrompt),
            SystemPrompt = systemPrompt,
            TestRunId = Guid.NewGuid()
        };

    [Fact]
    public void Assemble_ReturnsSystemPromptAsOutParameter()
    {
        var context = MakeContext(systemPrompt: "You are an expert API test engineer.");

        _sut.Assemble(context, out var systemPrompt);

        Assert.Equal("You are an expert API test engineer.", systemPrompt);
    }

    [Fact]
    public void Assemble_AllFourLayersPresent_WhenAllDataProvided()
    {
        // Use a context with memory examples and a custom prompt so all 4 user-turn layers are rendered.
        var context = new GeneratorContext
        {
            ParsedStory = MakeStory(
                entities: ["Order", "Customer"],
                actions: ["create", "submit"],
                edgeCases: ["duplicate order"]),
            MemoryRetrievalResult = MemoryWithScenarios(),
            ProjectConfig = MakeProject(customPrompt: "Always include auth header"),
            SystemPrompt = "You are an expert API test engineer.",
            TestRunId = Guid.NewGuid()
        };

        var prompt = _sut.Assemble(context, out _);

        // Layer 1: memory examples
        Assert.Contains("Reference Examples", prompt);
        Assert.Contains("Old story text", prompt);
        Assert.Contains("Pass rate: 0.90", prompt);
        // Layer 2: custom prompt
        Assert.Contains("Custom Instructions", prompt);
        Assert.Contains("Always include auth header", prompt);
        // Layer 3: testing strategy
        Assert.Contains("Testing Strategy", prompt);
        Assert.Contains("Focus on REST endpoints", prompt);
        // Layer 4: parsed story
        Assert.Contains("Create order", prompt);
        Assert.Contains("User creates a new order.", prompt);
        Assert.Contains("POST /orders returns 201", prompt);
        Assert.Contains("Order", prompt);
        Assert.Contains("create", prompt);
        Assert.Contains("duplicate order", prompt);
    }

    [Fact]
    public void Assemble_OmitsMemoryBlock_WhenScenariosEmpty()
    {
        var context = MakeContext(memory: EmptyMemory());

        var prompt = _sut.Assemble(context, out _);

        Assert.DoesNotContain("Reference Examples", prompt);
    }

    [Fact]
    public void Assemble_OmitsCustomPromptBlock_WhenCustomPromptNull()
    {
        var context = MakeContext(customPrompt: null);

        var prompt = _sut.Assemble(context, out _);

        Assert.DoesNotContain("Custom Instructions", prompt);
    }

    [Fact]
    public void Assemble_OmitsCustomPromptBlock_WhenCustomPromptEmpty()
    {
        var context = MakeContext(customPrompt: "");

        var prompt = _sut.Assemble(context, out _);

        Assert.DoesNotContain("Custom Instructions", prompt);
    }

    [Fact]
    public void Assemble_OmitsCustomPromptBlock_WhenCustomPromptWhitespace()
    {
        var context = MakeContext(customPrompt: "   ");

        var prompt = _sut.Assemble(context, out _);

        Assert.DoesNotContain("Custom Instructions", prompt);
    }

    [Fact]
    public void Assemble_LayerOrder_MemoryBeforeCustomPromptBeforeStrategyBeforeStory()
    {
        var context = new GeneratorContext
        {
            ParsedStory = MakeStory(),
            MemoryRetrievalResult = MemoryWithScenarios(),
            ProjectConfig = MakeProject(customPrompt: "Custom instructions here"),
            SystemPrompt = "You are an expert API test engineer.",
            TestRunId = Guid.NewGuid()
        };

        var prompt = _sut.Assemble(context, out _);

        var memoryIdx = prompt.IndexOf("Reference Examples", StringComparison.Ordinal);
        var customIdx = prompt.IndexOf("Custom Instructions", StringComparison.Ordinal);
        var strategyIdx = prompt.IndexOf("Testing Strategy", StringComparison.Ordinal);
        var storyIdx = prompt.IndexOf("## Story", StringComparison.Ordinal);

        Assert.True(memoryIdx < customIdx, "Memory examples should appear before custom prompt");
        Assert.True(customIdx < strategyIdx, "Custom prompt should appear before testing strategy");
        Assert.True(strategyIdx < storyIdx, "Testing strategy should appear before story");
    }

    [Fact]
    public void Assemble_FormatsMemoryExample_WithCorrectPattern()
    {
        var context = new GeneratorContext
        {
            ParsedStory = MakeStory(),
            MemoryRetrievalResult = MemoryWithScenarios(),
            ProjectConfig = MakeProject(),
            SystemPrompt = "You are an expert API test engineer.",
            TestRunId = Guid.NewGuid()
        };

        var prompt = _sut.Assemble(context, out _);

        // AC-010: Example N: Story: <storyText> / Scenarios: <scenarioText> / Pass rate: <passRate>
        Assert.Contains("Example 1: Story: Old story text / Scenarios:", prompt);
        Assert.Contains("Pass rate:", prompt);
    }
}
