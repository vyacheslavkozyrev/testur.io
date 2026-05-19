using Testurio.Core.Entities;

namespace Testurio.Core.Repositories;

/// <summary>
/// Persistence contract for <see cref="UserSubscription"/> documents stored in Cosmos DB.
/// </summary>
public interface IUserSubscriptionRepository
{
    /// <summary>
    /// Returns the subscription for the given user, or <c>null</c> if no record exists.
    /// </summary>
    Task<UserSubscription?> GetByUserIdAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates or replaces the subscription document for a user.
    /// </summary>
    Task<UserSubscription> UpsertAsync(UserSubscription subscription, CancellationToken cancellationToken = default);
}
