using Microsoft.Extensions.DependencyInjection;
using Testurio.Core.Interfaces;

namespace Testurio.Pipeline.StoryParser;

/// <summary>
/// DI registration for the Testurio.Pipeline.StoryParser project.
/// Call <see cref="AddStoryParser"/> from the host's service collection configuration.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registers <see cref="IStoryParser"/> as <see cref="StoryParserService"/> and all its dependencies.
    /// Requires <see cref="ILlmGenerationClient"/>, <see cref="IJiraApiClient"/>, <see cref="IADOClient"/>,
    /// and <see cref="ISecretResolver"/> to be registered separately (typically via AddInfrastructure).
    /// </summary>
    public static IServiceCollection AddStoryParser(this IServiceCollection services)
    {
        services.AddSingleton<TemplateChecker>();
        services.AddSingleton<DirectParser>();
        services.AddSingleton<AiStoryConverter>();
        services.AddSingleton<PmToolCommentPoster>();
        services.AddSingleton<StoryParserService>();
        services.AddSingleton<IStoryParser>(sp => sp.GetRequiredService<StoryParserService>());

        return services;
    }
}
