namespace Testurio.Core.Models;

/// <summary>
/// A prompt template document stored in the <c>PromptTemplates</c> Cosmos DB container.
/// Covers all pipeline stages (StoryParser, AgentRouter, ApiTestGenerator, UiE2eTestGenerator, ReportWriter).
/// <para>
/// The <see cref="Id"/> field matches <see cref="Stage"/> (e.g. <c>"story_parser"</c>),
/// so a point-read by <c>id</c> is sufficient to retrieve the correct document.
/// The <see cref="TemplateType"/> field is an alias for <see cref="Stage"/> kept for
/// Cosmos partition key path (<c>/templateType</c>) backward compatibility.
/// </para>
/// </summary>
public sealed record PromptTemplate
{
    /// <summary>
    /// Cosmos document identifier — same value as <see cref="Stage"/>.
    /// E.g. <c>"story_parser"</c> or <c>"api_test_generator"</c>.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// The pipeline stage this template belongs to.
    /// Valid MVP values: <c>"story_parser"</c>, <c>"agent_router"</c>,
    /// <c>"api_test_generator"</c>, <c>"ui_e2e_test_generator"</c>, <c>"report_writer"</c>.
    /// Doubles as the Cosmos document <c>id</c> so a single point-read suffices.
    /// </summary>
    public required string Stage { get; init; }

    /// <summary>
    /// Alias for <see cref="Stage"/> kept for Cosmos partition key path (<c>/templateType</c>)
    /// backward compatibility. Always equal to <see cref="Stage"/>.
    /// </summary>
    public required string TemplateType { get; init; }

    /// <summary>
    /// Monotonically incrementing version number. Starts at <c>1</c> on the seed document.
    /// Incremented by 1 on each admin PUT.
    /// </summary>
    public required int Version { get; init; }

    /// <summary>
    /// The full prompt text for this pipeline stage. Passed as the system prompt to the Claude API.
    /// Contains the complete instruction set previously hardcoded in the corresponding stage class.
    /// </summary>
    public required string Body { get; init; }

    /// <summary>
    /// Whether this template is active. The pipeline will not use a template with <c>IsActive == false</c>.
    /// </summary>
    public required bool IsActive { get; init; }

    /// <summary>
    /// UTC timestamp when this document was first created (seed run or initial insert).
    /// </summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// UTC timestamp of the most recent update to this document.
    /// Set to the seed run timestamp on creation; updated on each admin PUT.
    /// </summary>
    public required DateTimeOffset UpdatedAt { get; init; }
}
