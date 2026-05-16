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
    /// <remarks>
    /// Callers are responsible for ensuring the <paramref name="project"/> belongs to the
    /// correct user before invoking this method. In the API layer this is enforced by
    /// <c>ProjectAccessService</c> (ownership check via <c>GetByProjectIdAsync</c>).
    /// In the worker pipeline, tenant isolation is enforced at the Cosmos DB query level
    /// (partition key = userId), so any <see cref="Project"/> returned by the repository
    /// is already scoped to the correct user.
    /// </remarks>
    Task<ProjectAccessCredentials> ResolveAsync(Project project, CancellationToken cancellationToken = default);
}
