namespace Testurio.Core.Models;

public class TestScenarioStep
{
    public required string Title { get; init; }
    public required string Method { get; init; }
    public required string Path { get; init; }
    public string? RequestBody { get; init; }
    public int ExpectedStatusCode { get; init; }
    public string? ExpectedResponseSchema { get; init; }
}
