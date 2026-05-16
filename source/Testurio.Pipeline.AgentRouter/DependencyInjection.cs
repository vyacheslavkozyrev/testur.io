using Microsoft.Extensions.DependencyInjection;
using Testurio.Core.Interfaces;

namespace Testurio.Pipeline.AgentRouter;

/// <summary>
/// DI registration for the Testurio.Pipeline.AgentRouter project.
/// Call <see cref="AddAgentRouter"/> from the host's service collection configuration.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registers <see cref="IAgentRouter"/> as <see cref="AgentRouterService"/> and all its dependencies.
    /// Also registers <see cref="ITestGeneratorFactory"/> as <see cref="TestGeneratorFactory"/>.
    /// <para>
    /// Prerequisites (must be registered by the caller via <c>AddInfrastructure()</c>):
    /// <list type="bullet">
    ///   <item><see cref="ILlmGenerationClient"/> — for Claude classification calls</item>
    ///   <item><see cref="IJiraApiClient"/> — for skip comment posting to Jira</item>
    ///   <item><see cref="IADOClient"/> — for skip comment posting to ADO</item>
    ///   <item><see cref="ISecretResolver"/> — for resolving PM tool credentials from Key Vault</item>
    ///   <item><see cref="Testurio.Core.Repositories.ITestRunRepository"/> — for persisting routing metadata</item>
    /// </list>
    /// Concrete <see cref="ITestGeneratorAgent"/> implementations (keyed by <c>"api"</c> and <c>"ui_e2e"</c>)
    /// are registered by feature 0028 (Testurio.Pipeline.Generators) and must be present at runtime.
    /// </para>
    /// </summary>
    public static IServiceCollection AddAgentRouter(this IServiceCollection services)
    {
        services.AddSingleton<StoryClassifier>();
        services.AddSingleton<SkipCommentPoster>();
        services.AddSingleton<ITestGeneratorFactory, TestGeneratorFactory>();
        services.AddSingleton<IAgentRouter, AgentRouterService>();

        return services;
    }
}
