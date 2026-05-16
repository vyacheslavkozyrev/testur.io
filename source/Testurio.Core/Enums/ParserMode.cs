namespace Testurio.Core.Enums;

/// <summary>
/// Indicates how the StoryParser processed the incoming work item.
/// Written to the TestRun record after stage 1 completes (AC-020).
/// </summary>
public enum ParserMode
{
    /// <summary>Work item matched the Testurio template and was parsed directly without calling Claude.</summary>
    Direct,

    /// <summary>Work item did not match the template; Claude converted it to the ParsedStory schema.</summary>
    AiConverted
}
