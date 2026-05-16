using Testurio.Api.Services;
using Testurio.Core.Entities;
using Testurio.Core.Enums;
using Xunit;

namespace Testurio.UnitTests.Services;

public class WorkItemTypeFilterServiceTests
{
    private static WorkItemTypeFilterService CreateSut() => new();

    private static Project MakeProject(PMToolType? pmTool = PMToolType.Jira, string[]? allowedWorkItemTypes = null) => new()
    {
        UserId = "user1",
        Name = "Test",
        ProductUrl = "https://example.com",
        TestingStrategy = "smoke",
        PmTool = pmTool,
        AllowedWorkItemTypes = allowedWorkItemTypes,
    };

    // ─── GetEffectiveTypes ───────────────────────────────────────────────────

    [Fact]
    public void GetEffectiveTypes_WhenJiraAndNoCustomList_ReturnsJiraDefault()
    {
        var sut = CreateSut();
        var project = MakeProject(PMToolType.Jira);

        var result = sut.GetEffectiveTypes(project);

        Assert.Equal(["Story", "Bug"], result);
    }

    [Fact]
    public void GetEffectiveTypes_WhenAdoAndNoCustomList_ReturnsAdoDefault()
    {
        var sut = CreateSut();
        var project = MakeProject(PMToolType.Ado);

        var result = sut.GetEffectiveTypes(project);

        Assert.Equal(["User Story", "Bug"], result);
    }

    [Fact]
    public void GetEffectiveTypes_WhenCustomListConfigured_ReturnsCustomList()
    {
        var sut = CreateSut();
        var project = MakeProject(allowedWorkItemTypes: ["Epic", "Task"]);

        var result = sut.GetEffectiveTypes(project);

        Assert.Equal(["Epic", "Task"], result);
    }

    [Fact]
    public void GetEffectiveTypes_WhenNoPmToolAndNoCustomList_ReturnsFallbackDefault()
    {
        var sut = CreateSut();
        var project = MakeProject(pmTool: null);

        var result = sut.GetEffectiveTypes(project);

        Assert.Equal(["Story", "Bug"], result);
    }

    [Fact]
    public void GetEffectiveTypes_WhenEmptyArrayConfigured_ReturnsPmToolDefault()
    {
        var sut = CreateSut();
        var project = MakeProject(PMToolType.Jira, allowedWorkItemTypes: []);

        var result = sut.GetEffectiveTypes(project);

        // Empty array is treated as "not configured" — fall back to default (AC-016)
        Assert.Equal(["Story", "Bug"], result);
    }

    // ─── IsAllowed ───────────────────────────────────────────────────────────

    [Fact]
    public void IsAllowed_WhenTypeIsInDefaultList_ReturnsTrue()
    {
        var sut = CreateSut();
        var project = MakeProject(PMToolType.Jira);

        Assert.True(sut.IsAllowed(project, "Story"));
        Assert.True(sut.IsAllowed(project, "Bug"));
    }

    [Fact]
    public void IsAllowed_WhenTypeIsNotInDefaultList_ReturnsFalse()
    {
        var sut = CreateSut();
        var project = MakeProject(PMToolType.Jira);

        Assert.False(sut.IsAllowed(project, "Task"));
        Assert.False(sut.IsAllowed(project, "Sub-task"));
    }

    [Fact]
    public void IsAllowed_IsCaseSensitive()
    {
        var sut = CreateSut();
        var project = MakeProject(PMToolType.Jira);

        // "story" (lowercase) must not match "Story" (AC-013)
        Assert.False(sut.IsAllowed(project, "story"));
        Assert.False(sut.IsAllowed(project, "STORY"));
    }

    [Fact]
    public void IsAllowed_WhenCustomListConfigured_OnlyAllowsListedTypes()
    {
        var sut = CreateSut();
        var project = MakeProject(allowedWorkItemTypes: ["Epic"]);

        Assert.True(sut.IsAllowed(project, "Epic"));
        Assert.False(sut.IsAllowed(project, "Story"));
        Assert.False(sut.IsAllowed(project, "Bug"));
    }

    [Fact]
    public void IsAllowed_WhenAdoProject_UsesAdoDefaultList()
    {
        var sut = CreateSut();
        var project = MakeProject(PMToolType.Ado);

        Assert.True(sut.IsAllowed(project, "User Story"));
        Assert.True(sut.IsAllowed(project, "Bug"));
        Assert.False(sut.IsAllowed(project, "Story")); // Jira default — not ADO default
    }
}
