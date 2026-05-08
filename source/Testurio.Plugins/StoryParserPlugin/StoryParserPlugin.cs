namespace Testurio.Plugins.StoryParserPlugin;

public class StoryParserPlugin
{
    public ParsedStory Parse(string description, string acceptanceCriteria)
    {
        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("Story description must not be empty.", nameof(description));

        return new ParsedStory
        {
            Description = description.Trim(),
            AcceptanceCriteria = acceptanceCriteria?.Trim() ?? string.Empty
        };
    }

    public string FormatPromptInput(string description, string acceptanceCriteria)
    {
        var parsed = Parse(description, acceptanceCriteria);
        return $"## Story Description\n{parsed.Description}\n\n## Acceptance Criteria\n{parsed.AcceptanceCriteria}";
    }
}

public class ParsedStory
{
    public required string Description { get; init; }
    public required string AcceptanceCriteria { get; init; }
}
