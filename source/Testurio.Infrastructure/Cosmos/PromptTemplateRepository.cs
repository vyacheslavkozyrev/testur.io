using Microsoft.Azure.Cosmos;
using Testurio.Core.Interfaces;
using Testurio.Core.Models;

namespace Testurio.Infrastructure.Cosmos;

/// <summary>
/// Reads <see cref="PromptTemplate"/> documents from the <c>PromptTemplates</c> Cosmos DB container.
/// Partition key path is <c>/templateType</c> — each template type is its own logical partition.
/// </summary>
public sealed class PromptTemplateRepository : IPromptTemplateRepository
{
    private readonly Container _container;

    public PromptTemplateRepository(CosmosClient cosmosClient, string databaseName)
    {
        _container = cosmosClient.GetContainer(databaseName, "PromptTemplates");
    }

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">
    /// Thrown when the document with <c>id = templateType</c> does not exist in the container.
    /// </exception>
    public async Task<PromptTemplate> GetAsync(string templateType, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _container.ReadItemAsync<PromptTemplate>(
                templateType,
                new PartitionKey(templateType),
                cancellationToken: cancellationToken);

            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            throw new InvalidOperationException(
                $"PromptTemplate '{templateType}' not found in the PromptTemplates container. " +
                "Ensure the seeder has run and the document exists before starting the worker.",
                ex);
        }
    }
}
