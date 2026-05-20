using Microsoft.Extensions.DependencyInjection;
using Testurio.Core.Interfaces;

namespace Testurio.Pipeline.MemoryWriter;

/// <summary>
/// DI registration for the Testurio.Pipeline.MemoryWriter project.
/// Call <see cref="AddMemoryWriter"/> from the host's service collection configuration.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registers <see cref="MemoryWriterService"/> as <see cref="IMemoryWriterService"/> (transient).
    /// </summary>
    public static IServiceCollection AddMemoryWriter(this IServiceCollection services)
    {
        services.AddTransient<IMemoryWriterService, MemoryWriterService>();
        return services;
    }
}
