namespace Testurio.Core.Interfaces;

public interface IJiraApiClient
{
    Task<bool> PostCommentAsync(string baseUrl, string issueKey, string email, string apiToken, string commentBody, CancellationToken cancellationToken = default);
}
