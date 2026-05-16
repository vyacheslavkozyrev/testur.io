using System.ComponentModel.DataAnnotations;

namespace Testurio.Infrastructure.Options;

/// <summary>
/// Configuration for the Azure OpenAI service.
/// Used by <c>AzureOpenAIEmbeddingService</c> to generate story embeddings
/// for the MemoryRetrieval (stage 3) and MemoryWriter (stage 8) pipeline stages.
/// </summary>
public class AzureOpenAIOptions
{
    /// <summary>Azure OpenAI resource endpoint, e.g. <c>https://&lt;resource&gt;.openai.azure.com/</c>.</summary>
    [Required]
    public required string Endpoint { get; init; }

    /// <summary>Azure OpenAI API key. Loaded from Key Vault at startup via Managed Identity.</summary>
    [Required]
    public required string ApiKey { get; init; }

    /// <summary>Deployment name for the <c>text-embedding-3-small</c> model (1536 dimensions).</summary>
    [Required]
    public required string EmbeddingDeployment { get; init; }
}
