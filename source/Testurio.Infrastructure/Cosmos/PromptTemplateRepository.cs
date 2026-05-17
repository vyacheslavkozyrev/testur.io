using Microsoft.Azure.Cosmos;
using Testurio.Core.Interfaces;
using Testurio.Core.Models;

namespace Testurio.Infrastructure.Cosmos;

/// <summary>
/// Reads <see cref="PromptTemplate"/> documents from the <c>PromptTemplates</c> Cosmos DB container.
/// The container has no logical partition key on user data — templates are global/shared documents,
/// so a single partition key value (<c>"template"</c>) is used for all documents.
/// </summary>
public sealed class PromptTemplateRepository : IPromptTemplateRepository
{
    private const string PartitionKeyValue = "template";

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
                new PartitionKey(PartitionKeyValue),
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
