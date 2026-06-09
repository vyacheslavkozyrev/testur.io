namespace Testurio.Infrastructure.Options;

/// <summary>
/// Holds the Azure OpenAI API key loaded from Azure Key Vault at startup.
/// Key Vault secret name: <c>azure-openai-api-key</c>.
/// Development config key: <c>AzureOpenAI__ApiKey</c>.
/// Registered as a singleton — never bind from <c>IOptions&lt;T&gt;</c>.
/// </summary>
public sealed class AzureOpenAISecrets
{
    /// <summary>Azure OpenAI API key.</summary>
    public string ApiKey { get; init; } = string.Empty;
}
