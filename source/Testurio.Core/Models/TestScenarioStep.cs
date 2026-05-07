namespace Testurio.Core.Models;

public class TestScenarioStep
{
    public required int Order { get; init; }
    public required string Description { get; init; }
    public required string ExpectedResult { get; init; }
}
