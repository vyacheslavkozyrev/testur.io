namespace Testurio.Infrastructure.Options;

/// <summary>
/// Holds the Anthropic Claude API key loaded from Azure Key Vault at startup.
/// Key Vault secret name: <c>anthropic-api-key</c>.
/// Development config key: <c>Claude__ApiKey</c>.
/// Registered as a singleton — never bind from <c>IOptions&lt;T&gt;</c>.
/// </summary>
public sealed class AnthropicSecrets
{
    /// <summary>Anthropic API key (<c>sk-ant-*</c>).</summary>
    public string ApiKey { get; init; } = string.Empty;
}
