using Microsoft.Azure.Cosmos;
using Testurio.Core.Entities;
using Testurio.Core.Repositories;

namespace Testurio.Infrastructure.Cosmos;

/// <summary>
/// Cosmos DB implementation of <see cref="IUserSubscriptionRepository"/>.
/// Documents are stored in the <c>UserSubscriptions</c> container, partitioned by <c>userId</c>.
/// </summary>
public class UserSubscriptionRepository : IUserSubscriptionRepository
{
    private readonly Container _container;

    public UserSubscriptionRepository(CosmosClient cosmosClient, string databaseName)
    {
        _container = cosmosClient.GetContainer(databaseName, "UserSubscriptions");
    }

    /// <inheritdoc/>
    public async Task<UserSubscription?> GetByUserIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _container.ReadItemAsync<UserSubscription>(
                userId,
                new PartitionKey(userId),
                cancellationToken: cancellationToken);
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    /// <inheritdoc/>
    public async Task<UserSubscription> UpsertAsync(UserSubscription subscription, CancellationToken cancellationToken = default)
    {
        var response = await _container.UpsertItemAsync(
            subscription,
            new PartitionKey(subscription.UserId),
            cancellationToken: cancellationToken);
        return response.Resource;
    }
}
