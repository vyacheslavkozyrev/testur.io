namespace Testurio.Infrastructure.Options;

/// <summary>
/// Holds secret connection strings for core Azure infrastructure services.
/// In production these are loaded from Azure Key Vault at startup via <c>AddInfrastructureSecrets</c>.
/// In development they are read from local configuration (<c>.env</c> / user secrets).
/// Registered as a singleton — never bind from <c>IOptions&lt;T&gt;</c>.
/// </summary>
public sealed class InfrastructureSecrets
{
    /// <summary>
    /// Azure Cosmos DB connection string.
    /// Key Vault secret name: <c>cosmos-connection-string</c>.
    /// Development config key: <c>Infrastructure__CosmosConnectionString</c>.
    /// </summary>
    public string CosmosConnectionString { get; init; } = string.Empty;

    /// <summary>
    /// Azure Service Bus connection string.
    /// Key Vault secret name: <c>servicebus-connection-string</c>.
    /// Development config key: <c>Infrastructure__ServiceBusConnectionString</c>.
    /// </summary>
    public string ServiceBusConnectionString { get; init; } = string.Empty;

    /// <summary>
    /// Azure Blob Storage connection string.
    /// Key Vault secret name: <c>blob-storage-connection-string</c>.
    /// Development config key: <c>Infrastructure__BlobStorageConnectionString</c>.
    /// </summary>
    public string BlobStorageConnectionString { get; init; } = string.Empty;
}
