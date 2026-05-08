using Testurio.Core.Models;

namespace Testurio.Core.Interfaces;

public interface IJiraStoryClient
{
    Task<JiraStoryContent?> GetStoryContentAsync(string baseUrl, string issueKey, string email, string apiToken, CancellationToken cancellationToken = default);
}
