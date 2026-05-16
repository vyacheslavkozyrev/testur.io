using Azure;
using Azure.AI.OpenAI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI.Embeddings;
using Testurio.Core.Interfaces;
using Testurio.Infrastructure.Options;

namespace Testurio.Infrastructure.Embedding;

/// <summary>
/// Implements <see cref="IEmbeddingService"/> using the Azure OpenAI <c>text-embedding-3-small</c>
/// model (1536 dimensions). Called by the MemoryRetrieval (stage 3) and MemoryWriter (stage 8)
/// pipeline stages to convert story text into vectors for Cosmos DiskANN vector search.
/// </summary>
public sealed partial class AzureOpenAIEmbeddingService : IEmbeddingService
{
    private readonly EmbeddingClient _embeddingClient;
    private readonly ILogger<AzureOpenAIEmbeddingService> _logger;

    public AzureOpenAIEmbeddingService(
        IOptions<AzureOpenAIOptions> options,
        ILogger<AzureOpenAIEmbeddingService> logger)
    {
        var opts = options.Value;
        var azureClient = new AzureOpenAIClient(
            new Uri(opts.Endpoint),
            new AzureKeyCredential(opts.ApiKey));

        _embeddingClient = azureClient.GetEmbeddingClient(opts.EmbeddingDeployment);
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default)
    {
        LogEmbedding(_logger, text.Length);

        var options = new EmbeddingGenerationOptions { Dimensions = 1536 };
        var response = await _embeddingClient.GenerateEmbeddingAsync(text, options, cancellationToken);
        var embedding = response.Value;

        var vector = embedding.ToFloats();
        return vector.ToArray();
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Generating embedding for text of length {TextLength}")]
    private static partial void LogEmbedding(ILogger logger, int textLength);
}
