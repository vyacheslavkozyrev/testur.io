using Microsoft.Extensions.DependencyInjection;
using Testurio.Core.Interfaces;

namespace Testurio.Pipeline.Executors;

/// <summary>
/// DI registration for the Testurio.Pipeline.Executors project.
/// Call <see cref="AddExecutors"/> from the host's service collection configuration.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registers <see cref="HttpExecutor"/> as <see cref="IHttpExecutor"/>,
    /// <see cref="PlaywrightExecutor"/> as <see cref="IPlaywrightExecutor"/>,
    /// and <see cref="ExecutorRouter"/> as <see cref="IExecutorRouter"/> as singletons.
    /// <para>
    /// Prerequisites (must be registered by the caller):
    /// <list type="bullet">
    ///   <item><see cref="IProjectAccessCredentialProvider"/> — for environment access credential resolution</item>
    ///   <item><see cref="IScreenshotStorage"/> — for screenshot upload on assertion failures</item>
    ///   <item><see cref="IHttpClientFactory"/> — for <see cref="HttpExecutor"/> HTTP calls</item>
    /// </list>
    /// </para>
    /// </summary>
    public static IServiceCollection AddExecutors(this IServiceCollection services)
    {
        services.AddSingleton<IHttpExecutor, HttpExecutor>();
        services.AddSingleton<IPlaywrightExecutor, PlaywrightExecutor>();
        services.AddSingleton<IExecutorRouter, ExecutorRouter>();

        return services;
    }
}
