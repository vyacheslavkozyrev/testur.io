using Microsoft.Extensions.DependencyInjection;
using Testurio.Core.Interfaces;

namespace Testurio.Pipeline.MemoryRetrieval;

/// <summary>
/// DI registration for the Testurio.Pipeline.MemoryRetrieval project.
/// Call <see cref="AddMemoryRetrieval"/> from the host's service collection configuration.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registers <see cref="IMemoryRetrievalService"/> as <see cref="MemoryRetrievalService"/>.
    /// <para>
    /// Prerequisites (must be registered by the caller via <c>AddInfrastructure()</c> and
    /// <c>AddAzureOpenAI()</c>):
    /// <list type="bullet">
    ///   <item><see cref="IEmbeddingService"/> — for story embedding generation</item>
    ///   <item><see cref="ITestMemoryRepository"/> — for Cosmos DiskANN vector search</item>
    /// </list>
    /// </para>
    /// </summary>
    public static IServiceCollection AddMemoryRetrieval(this IServiceCollection services)
    {
        services.AddSingleton<IMemoryRetrievalService, MemoryRetrievalService>();
        return services;
    }
}
