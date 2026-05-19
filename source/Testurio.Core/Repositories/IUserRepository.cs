using Testurio.Core.Entities;

namespace Testurio.Core.Repositories;

/// <summary>
/// Persistence contract for the <c>Users</c> Cosmos container.
/// All operations are scoped to a single partition (<c>userId</c>) — point reads/writes only.
/// </summary>
public interface IUserRepository
{
    /// <summary>
    /// Returns the user document for <paramref name="userId"/>, or <c>null</c> if none exists.
    /// </summary>
    Task<UserDocument?> GetByUserIdAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Upserts the user document (creates if absent, replaces if present).
    /// The document <c>id</c> must equal <c>userId</c>.
    /// </summary>
    Task<UserDocument> UpsertAsync(UserDocument document, CancellationToken cancellationToken = default);
}
