using Testurio.Core.Entities;
using Testurio.Core.Models;

namespace Testurio.Core.Interfaces;

public interface IApiTestAuthCredentialProvider
{
    /// <summary>
    /// Resolves the API test authentication credentials for the given project.
    /// Returns a typed <see cref="ApiTestAuthCredentials"/> discriminated union.
    /// Throws <see cref="Exceptions.CredentialRetrievalException"/> if Key Vault is unreachable or a secret URI is invalid.
    /// </summary>
    Task<ApiTestAuthCredentials> ResolveAsync(Project project, CancellationToken ct = default);
}
