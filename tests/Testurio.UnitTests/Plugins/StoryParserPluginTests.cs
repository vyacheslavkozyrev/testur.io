using Testurio.Plugins.StoryParserPlugin;
using Xunit;

namespace Testurio.UnitTests.Plugins;

public class StoryParserPluginTests
{
    private static StoryParserPlugin CreateSut() => new();

    [Fact]
    public void Parse_WithValidDescriptionAndAc_ReturnsParsedStory()
    {
        var sut = CreateSut();

        var result = sut.Parse("User story description", "AC-001: something");

        Assert.Equal("User story description", result.Description);
        Assert.Equal("AC-001: something", result.AcceptanceCriteria);
    }

    [Fact]
    public void Parse_TrimsWhitespace()
    {
        var sut = CreateSut();

        var result = sut.Parse("  description with spaces  ", "  ac with spaces  ");

        Assert.Equal("description with spaces", result.Description);
        Assert.Equal("ac with spaces", result.AcceptanceCriteria);
    }

    [Fact]
    public void Parse_WithEmptyDescription_ThrowsArgumentException()
    {
        var sut = CreateSut();

        Assert.Throws<ArgumentException>(() => sut.Parse(string.Empty, "some AC"));
    }

    [Fact]
    public void Parse_WithWhitespaceOnlyDescription_ThrowsArgumentException()
    {
        var sut = CreateSut();

        Assert.Throws<ArgumentException>(() => sut.Parse("   ", "some AC"));
    }

    [Fact]
    public void Parse_WithNullAcceptanceCriteria_ReturnsEmptyAc()
    {
        var sut = CreateSut();

        var result = sut.Parse("Valid description", null!);

        Assert.Equal("Valid description", result.Description);
        Assert.Equal(string.Empty, result.AcceptanceCriteria);
    }

    [Fact]
    public void FormatPromptInput_ContainsBothSections()
    {
        var sut = CreateSut();

        var output = sut.FormatPromptInput("My story description", "AC-001: do something");

        Assert.Contains("## Story Description", output);
        Assert.Contains("My story description", output);
        Assert.Contains("## Acceptance Criteria", output);
        Assert.Contains("AC-001: do something", output);
    }

    [Fact]
    public void FormatPromptInput_WithEmptyAc_StillIncludesSection()
    {
        var sut = CreateSut();

        var output = sut.FormatPromptInput("My story description", string.Empty);

        Assert.Contains("## Acceptance Criteria", output);
    }
}
