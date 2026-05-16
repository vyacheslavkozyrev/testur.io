using Testurio.Core.Enums;
using Testurio.Core.Models;
using Testurio.Pipeline.StoryParser;

namespace Testurio.UnitTests.Pipeline;

public class TemplateCheckerTests
{
    private static WorkItem MakeWorkItem(string title, string description, string acceptanceCriteria) =>
        new()
        {
            Title = title,
            Description = description,
            AcceptanceCriteria = acceptanceCriteria,
            PmToolType = PMToolType.Jira,
            IssueKey = "PROJ-1"
        };

    private readonly TemplateChecker _sut = new();

    [Fact]
    public void IsConformant_AllFieldsPresent_ReturnsTrue()
    {
        var workItem = MakeWorkItem("Add to cart", "User can add an item to their cart.", "- Cart total updates correctly");

        Assert.True(_sut.IsConformant(workItem));
    }

    [Fact]
    public void IsConformant_MissingTitle_ReturnsFalse()
    {
        var workItem = MakeWorkItem(string.Empty, "User can add an item.", "- At least one AC");

        Assert.False(_sut.IsConformant(workItem));
    }

    [Fact]
    public void IsConformant_WhitespaceTitle_ReturnsFalse()
    {
        var workItem = MakeWorkItem("   ", "User can add an item.", "- At least one AC");

        Assert.False(_sut.IsConformant(workItem));
    }

    [Fact]
    public void IsConformant_MissingDescription_ReturnsFalse()
    {
        var workItem = MakeWorkItem("Add to cart", string.Empty, "- At least one AC");

        Assert.False(_sut.IsConformant(workItem));
    }

    [Fact]
    public void IsConformant_WhitespaceDescription_ReturnsFalse()
    {
        var workItem = MakeWorkItem("Add to cart", "   ", "- At least one AC");

        Assert.False(_sut.IsConformant(workItem));
    }

    [Fact]
    public void IsConformant_MissingAcceptanceCriteria_ReturnsFalse()
    {
        var workItem = MakeWorkItem("Add to cart", "User can add an item.", string.Empty);

        Assert.False(_sut.IsConformant(workItem));
    }

    [Fact]
    public void IsConformant_WhitespaceAcceptanceCriteria_ReturnsFalse()
    {
        var workItem = MakeWorkItem("Add to cart", "User can add an item.", "   ");

        Assert.False(_sut.IsConformant(workItem));
    }
}
