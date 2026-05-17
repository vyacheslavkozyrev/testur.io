namespace Testurio.Core.Models;

/// <summary>
/// A generator prompt template stored in the <c>PromptTemplates</c> Cosmos DB container.
/// Loaded once per pipeline run (stage 4) via <c>IPromptTemplateRepository</c> before
/// constructing <c>GeneratorContext</c> instances.
/// <para>
/// The <see cref="Id"/> field matches <see cref="TemplateType"/> (e.g. <c>"api_test_generator"</c>),
/// so a point-read by <c>id</c> is sufficient to retrieve the correct document.
/// </para>
/// </summary>
public sealed record PromptTemplate
{
    /// <summary>
    /// Cosmos document identifier — same value as <see cref="TemplateType"/>.
    /// E.g. <c>"api_test_generator"</c> or <c>"ui_e2e_test_generator"</c>.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Identifies which generator this template belongs to.
    /// Valid MVP values: <c>"api_test_generator"</c>, <c>"ui_e2e_test_generator"</c>.
    /// </summary>
    public required string TemplateType { get; init; }

    /// <summary>
    /// Semantic version of this template document, e.g. <c>"1.0.0"</c>.
    /// Incremented when the prompt content is updated.
    /// </summary>
    public required string Version { get; init; }

    /// <summary>
    /// System-level instructions sent as the <c>system</c> field in the Claude API request.
    /// Establishes the agent persona and global behaviour constraints.
    /// </summary>
    public required string SystemPrompt { get; init; }

    /// <summary>
    /// User-turn instruction appended last in the assembled prompt.
    /// May contain the <c>{{maxScenarios}}</c> placeholder, which is substituted with
    /// <see cref="MaxScenarios"/> before the prompt is sent to Claude.
    /// </summary>
    public required string GeneratorInstruction { get; init; }

    /// <summary>
    /// Maximum number of scenarios the generator is allowed to produce.
    /// Substituted into <see cref="GeneratorInstruction"/> at <c>{{maxScenarios}}</c>.
    /// Seeded values: 10 for <c>api_test_generator</c>, 5 for <c>ui_e2e_test_generator</c>.
    /// </summary>
    public required int MaxScenarios { get; init; }
}
