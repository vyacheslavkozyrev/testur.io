namespace Testurio.Core.Interfaces;

/// <summary>
/// Contract for text embedding generation.
/// Used by the MemoryRetrieval (stage 3) and MemoryWriter (stage 8) pipeline stages to convert
/// story text into a fixed-length float vector for Cosmos DiskANN vector search.
/// </summary>
public interface IEmbeddingService
{
    /// <summary>
    /// Generates a semantic embedding vector for the given text.
    /// </summary>
    /// <param name="text">The text to embed. Must not be null or empty.</param>
    /// <param name="cancellationToken">Cancellation token forwarded to the underlying API call.</param>
    /// <returns>
    /// A <c>float[1536]</c> embedding vector produced by Azure OpenAI <c>text-embedding-3-small</c>.
    /// </returns>
    Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default);
}
