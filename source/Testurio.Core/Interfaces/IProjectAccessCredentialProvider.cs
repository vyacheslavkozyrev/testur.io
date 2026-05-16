using Testurio.Core.Entities;
using Testurio.Core.Models;

namespace Testurio.Core.Interfaces;

public interface IProjectAccessCredentialProvider
{
    /// <summary>
    /// Resolves the access credentials for the given project.
    /// Returns a typed <see cref="ProjectAccessCredentials"/> discriminated union.
    /// Throws <see cref="Exceptions.CredentialRetrievalException"/> if Key Vault is unreachable or the secret URI is invalid.
    /// </summary>
    Task<ProjectAccessCredentials> ResolveAsync(Project project, CancellationToken cancellationToken = default);
}
