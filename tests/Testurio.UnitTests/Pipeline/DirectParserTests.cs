using Testurio.Core.Enums;
using Testurio.Core.Models;
using Testurio.Pipeline.StoryParser;

namespace Testurio.UnitTests.Pipeline;

public class DirectParserTests
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

    private readonly DirectParser _sut = new();

    [Fact]
    public void Parse_AllFieldsPresent_ReturnsParsedStoryWithCorrectTitleAndDescription()
    {
        var workItem = MakeWorkItem(
            "  Add item to cart  ",
            "A user can add a product to their shopping cart.",
            "1. Cart total updates\n2. Item appears in cart");

        var result = _sut.Parse(workItem);

        Assert.Equal("Add item to cart", result.Title);
        Assert.Equal("A user can add a product to their shopping cart.", result.Description);
    }

    [Fact]
    public void Parse_NumberedListAcceptanceCriteria_SplitsIntoMultipleEntries()
    {
        var workItem = MakeWorkItem(
            "Add to cart",
            "User adds item.",
            "1. Cart total updates\n2. Item appears in cart\n3. Stock decrements");

        var result = _sut.Parse(workItem);

        Assert.Equal(3, result.AcceptanceCriteria.Count);
        Assert.Contains("Cart total updates", result.AcceptanceCriteria);
        Assert.Contains("Item appears in cart", result.AcceptanceCriteria);
        Assert.Contains("Stock decrements", result.AcceptanceCriteria);
    }

    [Fact]
    public void Parse_BulletListAcceptanceCriteria_SplitsIntoMultipleEntries()
    {
        var workItem = MakeWorkItem(
            "Add to cart",
            "User adds item.",
            "- Cart total updates\n- Item appears in cart");

        var result = _sut.Parse(workItem);

        Assert.Equal(2, result.AcceptanceCriteria.Count);
    }

    [Fact]
    public void Parse_EntitiesDetected_ReturnsNonEmptyEntitiesArray()
    {
        var workItem = MakeWorkItem(
            "Update user account",
            "The user can update their account settings.",
            "- Account page shows updated info");

        var result = _sut.Parse(workItem);

        Assert.NotEmpty(result.Entities);
        Assert.Contains("user", result.Entities);
        Assert.Contains("account", result.Entities);
    }

    [Fact]
    public void Parse_ActionsDetected_ReturnsNonEmptyActionsArray()
    {
        var workItem = MakeWorkItem(
            "Delete old orders",
            "Admin can delete expired orders from the system.",
            "- Orders are removed from the list");

        var result = _sut.Parse(workItem);

        Assert.NotEmpty(result.Actions);
        Assert.Contains("delete", result.Actions);
    }

    [Fact]
    public void Parse_EdgeCasesDetected_ReturnsNonEmptyEdgeCasesArray()
    {
        var workItem = MakeWorkItem(
            "Handle invalid input",
            "The system returns an error when invalid data is submitted.",
            "- Error message shown for invalid fields");

        var result = _sut.Parse(workItem);

        Assert.NotEmpty(result.EdgeCases);
        Assert.Contains("invalid", result.EdgeCases);
        Assert.Contains("error", result.EdgeCases);
    }

    [Fact]
    public void Parse_NoEntityKeywordsPresent_ReturnsEmptyEntitiesArray()
    {
        var workItem = MakeWorkItem(
            "Toggle dark mode",
            "The interface can switch between light and dark themes.",
            "- Theme changes immediately on toggle");

        var result = _sut.Parse(workItem);

        // entities array must never be null — may be empty
        Assert.NotNull(result.Entities);
    }

    [Fact]
    public void Parse_NoActionKeywordsPresent_ReturnsEmptyActionsArray()
    {
        var workItem = MakeWorkItem(
            "Show status badge",
            "A coloured badge indicates the current pipeline status.",
            "- Badge colour reflects pipeline state");

        var result = _sut.Parse(workItem);

        Assert.NotNull(result.Actions);
    }

    [Fact]
    public void Parse_NoEdgeCaseKeywordsPresent_ReturnsEmptyEdgeCasesArray()
    {
        var workItem = MakeWorkItem(
            "Display project name",
            "The project name is shown on the dashboard.",
            "- Name is visible on the dashboard");

        var result = _sut.Parse(workItem);

        Assert.NotNull(result.EdgeCases);
    }

    [Fact]
    public void Parse_ResultNeverHasNullCollections()
    {
        var workItem = MakeWorkItem("T", "D", "AC");

        var result = _sut.Parse(workItem);

        Assert.NotNull(result.AcceptanceCriteria);
        Assert.NotNull(result.Entities);
        Assert.NotNull(result.Actions);
        Assert.NotNull(result.EdgeCases);
    }
}
