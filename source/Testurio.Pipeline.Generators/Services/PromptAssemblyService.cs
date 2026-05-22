using System.Text;
using Testurio.Core.Models;

namespace Testurio.Pipeline.Generators.Services;

/// <summary>
/// Assembles the full Claude user-turn prompt for a generator agent from five ordered context layers.
/// The system prompt is supplied separately by the caller via <see cref="GeneratorContext.SystemPrompt"/>
/// (resolved from Cosmos by <c>IPromptTemplateService</c>) and passed to the Claude API's
/// <c>system</c> field independently.
/// The resulting prompt string is passed directly to the Claude streaming API; it is never
/// logged, persisted, or included in any error response.
/// </summary>
/// <remarks>
/// Layer order (user-turn):
/// <list type="number">
///   <item>Few-shot memory examples — from <see cref="MemoryRetrievalResult.Scenarios"/> (omitted when empty)</item>
///   <item>Project custom prompt — from <c>Project.CustomPrompt</c> (omitted when null/empty)</item>
///   <item>Testing strategy — from <c>Project.TestingStrategy</c></item>
///   <item>Parsed story — full text of title, description, AC, entities, actions, edge cases</item>
///   <item>System prompt is passed to the Claude API's <c>system</c> field via <see cref="GeneratorContext.SystemPrompt"/></item>
/// </list>
/// </remarks>
public sealed class PromptAssemblyService
{
    /// <summary>
    /// Assembles the user-turn prompt from all available context layers.
    /// The system prompt is read from <see cref="GeneratorContext.SystemPrompt"/> and returned
    /// separately so the caller can pass it to the Claude API's <c>system</c> field.
    /// </summary>
    /// <param name="context">Generator context carrying all required input data.</param>
    /// <param name="systemPrompt">
    /// Output: the system-level instruction string from <see cref="GeneratorContext.SystemPrompt"/>,
    /// resolved from Cosmos via <c>IPromptTemplateService</c> before the context was constructed.
    /// </param>
    /// <returns>The assembled user-turn prompt string.</returns>
    public string Assemble(GeneratorContext context, out string systemPrompt)
    {
        // System prompt comes from the resolved Body string on the context.
        systemPrompt = context.SystemPrompt;

        var sb = new StringBuilder();

        // Layer 1: few-shot memory examples — omitted entirely when empty.
        if (context.MemoryRetrievalResult.Scenarios.Count > 0)
        {
            sb.AppendLine("## Reference Examples");
            for (int i = 0; i < context.MemoryRetrievalResult.Scenarios.Count; i++)
            {
                var scenario = context.MemoryRetrievalResult.Scenarios[i];
                sb.AppendLine(
                    $"Example {i + 1}: Story: {scenario.StoryText} / " +
                    $"Scenarios: {scenario.ScenarioText} / " +
                    $"Pass rate: {scenario.PassRate:F2}");
            }
            sb.AppendLine();
        }

        // Layer 2: project custom prompt — omitted entirely when null or empty.
        if (!string.IsNullOrWhiteSpace(context.ProjectConfig.CustomPrompt))
        {
            sb.AppendLine("## Custom Instructions");
            sb.AppendLine(context.ProjectConfig.CustomPrompt);
            sb.AppendLine();
        }

        // Layer 3: testing strategy.
        sb.AppendLine("## Testing Strategy");
        sb.AppendLine(context.ProjectConfig.TestingStrategy);
        sb.AppendLine();

        // Layer 4: parsed story — full text of all story fields.
        AppendParsedStory(sb, context.ParsedStory);

        return sb.ToString();
    }

    private static void AppendParsedStory(StringBuilder sb, ParsedStory story)
    {
        sb.AppendLine("## Story");
        sb.AppendLine($"Title: {story.Title}");
        sb.AppendLine();
        sb.AppendLine($"Description: {story.Description}");
        sb.AppendLine();

        if (story.AcceptanceCriteria.Count > 0)
        {
            sb.AppendLine("Acceptance Criteria:");
            foreach (var ac in story.AcceptanceCriteria)
                sb.AppendLine($"- {ac}");
            sb.AppendLine();
        }

        if (story.Entities.Count > 0)
        {
            sb.AppendLine($"Entities: {string.Join(", ", story.Entities)}");
            sb.AppendLine();
        }

        if (story.Actions.Count > 0)
        {
            sb.AppendLine($"Actions: {string.Join(", ", story.Actions)}");
            sb.AppendLine();
        }

        if (story.EdgeCases.Count > 0)
        {
            sb.AppendLine($"Edge Cases: {string.Join(", ", story.EdgeCases)}");
            sb.AppendLine();
        }
    }
}
