using Microsoft.Azure.Cosmos;
using Testurio.Core.Entities;
using Testurio.Core.Repositories;

namespace Testurio.Infrastructure.Cosmos;

public class UserRepository : IUserRepository
{
    private readonly Container _container;

    public UserRepository(CosmosClient cosmosClient, string databaseName)
    {
        _container = cosmosClient.GetContainer(databaseName, "Users");
    }

    public async Task<UserDocument?> GetByUserIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _container.ReadItemAsync<UserDocument>(
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

    public async Task<UserDocument> UpsertAsync(UserDocument document, CancellationToken cancellationToken = default)
    {
        var response = await _container.UpsertItemAsync(
            document,
            new PartitionKey(document.UserId),
            cancellationToken: cancellationToken);
        return response.Resource;
    }
}
