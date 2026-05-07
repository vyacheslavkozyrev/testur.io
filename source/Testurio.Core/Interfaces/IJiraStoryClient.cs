namespace Testurio.Core.Interfaces;

public interface IJiraStoryClient
{
    Task<JiraStoryContent?> GetStoryContentAsync(string baseUrl, string issueKey, string email, string apiToken, CancellationToken cancellationToken = default);
}

public class JiraStoryContent
{
    public required string Description { get; init; }
    public required string AcceptanceCriteria { get; init; }
}
