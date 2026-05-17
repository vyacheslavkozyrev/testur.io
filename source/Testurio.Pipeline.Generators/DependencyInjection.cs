using Microsoft.Extensions.DependencyInjection;
using Testurio.Core.Interfaces;
using Testurio.Pipeline.Generators.Services;

namespace Testurio.Pipeline.Generators;

/// <summary>
/// DI registration for the Testurio.Pipeline.Generators project.
/// Call <see cref="AddGenerators"/> from the host's service collection configuration.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registers <see cref="ApiTestGeneratorAgent"/> and <see cref="UiE2eTestGeneratorAgent"/>
    /// as keyed <see cref="ITestGeneratorAgent"/> services (<c>"api"</c> and <c>"ui_e2e"</c>),
    /// and registers <see cref="PromptAssemblyService"/> as a singleton.
    /// <para>
    /// Prerequisites (must be registered by the caller):
    /// <list type="bullet">
    ///   <item><see cref="ILlmGenerationClient"/> — for Claude API calls</item>
    /// </list>
    /// The keyed service keys match <see cref="Pipeline.AgentRouter.TestGeneratorFactory.ApiKey"/>
    /// and <see cref="Pipeline.AgentRouter.TestGeneratorFactory.UiE2eKey"/> constants in feature 0026.
    /// </para>
    /// </summary>
    public static IServiceCollection AddGenerators(this IServiceCollection services)
    {
        services.AddSingleton<PromptAssemblyService>();

        services.AddKeyedSingleton<ITestGeneratorAgent, ApiTestGeneratorAgent>("api");
        services.AddKeyedSingleton<ITestGeneratorAgent, UiE2eTestGeneratorAgent>("ui_e2e");

        return services;
    }
}
