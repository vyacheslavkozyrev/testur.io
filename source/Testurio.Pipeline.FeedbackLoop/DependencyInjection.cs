using Microsoft.Extensions.DependencyInjection;
using Testurio.Core.Interfaces;

namespace Testurio.Pipeline.FeedbackLoop;

/// <summary>
/// DI registration for the Testurio.Pipeline.FeedbackLoop project.
/// Call <see cref="AddFeedbackLoop"/> from the host's service collection configuration.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registers <see cref="FeedbackLoop"/> as <see cref="IFeedbackLoop"/> as a singleton.
    /// <para>
    /// Prerequisites (must be registered by the caller):
    /// <list type="bullet">
    ///   <item><see cref="ITestRunRepository"/> — for resolving last run test types (AC-007)</item>
    ///   <item><see cref="IProjectRepository"/> — for loading project credentials</item>
    ///   <item><see cref="IEmbeddingService"/> — for <c>text-embedding-3-small</c> embedding (AC-012)</item>
    ///   <item><see cref="ITestMemoryRepository"/> — for <c>UpsertFeedbackAsync</c> (AC-013)</item>
    ///   <item><see cref="IJiraApiClient"/> — for Jira confirmation comment posting (AC-019)</item>
    ///   <item><see cref="IADOClient"/> — for ADO confirmation comment posting (AC-019)</item>
    ///   <item><see cref="ISecretResolver"/> — for Key Vault credential resolution</item>
    /// </list>
    /// </para>
    /// </summary>
    public static IServiceCollection AddFeedbackLoop(this IServiceCollection services)
    {
        services.AddSingleton<IFeedbackLoop, FeedbackLoop>();

        return services;
    }
}
