using Microsoft.Azure.Cosmos;
using Testurio.Core.Interfaces;
using Testurio.Core.Models;

namespace Testurio.Infrastructure.Cosmos;

/// <summary>
/// Reads and writes <see cref="PromptTemplate"/> documents in the <c>PromptTemplates</c> Cosmos DB container.
/// Partition key path is <c>/templateType</c> — each stage key is its own logical partition.
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
    /// Thrown when the document with <c>id = stage</c> does not exist in the container.
    /// </exception>
    public async Task<PromptTemplate> GetAsync(string stage, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _container.ReadItemAsync<PromptTemplate>(
                stage,
                new PartitionKey(stage),
                cancellationToken: cancellationToken);

            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            throw new InvalidOperationException(
                $"PromptTemplate '{stage}' not found in the PromptTemplates container. " +
                "Ensure the seeder has run and the document exists before starting the worker.",
                ex);
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PromptTemplate>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var query = new QueryDefinition("SELECT * FROM c");
        var iterator = _container.GetItemQueryIterator<PromptTemplate>(query);

        var results = new List<PromptTemplate>();
        while (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync(cancellationToken);
            results.AddRange(page);
        }

        return results.AsReadOnly();
    }

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">
    /// Thrown when no document with <c>id = template.Stage</c> exists in the container.
    /// </exception>
    public async Task UpdateAsync(PromptTemplate template, CancellationToken cancellationToken = default)
    {
        try
        {
            await _container.ReplaceItemAsync(
                template,
                template.Stage,
                new PartitionKey(template.TemplateType),
                cancellationToken: cancellationToken);
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            throw new InvalidOperationException(
                $"PromptTemplate '{template.Stage}' not found in the PromptTemplates container. " +
                "Cannot update a document that does not exist — ensure the seeder has run first.",
                ex);
        }
    }
}
