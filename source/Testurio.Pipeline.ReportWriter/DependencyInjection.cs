using Microsoft.Extensions.DependencyInjection;
using Testurio.Core.Interfaces;

namespace Testurio.Pipeline.ReportWriter;

/// <summary>
/// DI registration for the Testurio.Pipeline.ReportWriter project.
/// Call <see cref="AddReportWriter"/> from the host's service collection configuration.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registers <see cref="ReportWriter"/> as <see cref="IReportWriter"/> as a singleton.
    /// <para>
    /// Prerequisites (must be registered by the caller):
    /// <list type="bullet">
    ///   <item><see cref="ILlmGenerationClient"/> — for Claude API calls</item>
    ///   <item><see cref="IJiraApiClient"/> — for Jira comment posting</item>
    ///   <item><see cref="IADOClient"/> — for ADO comment posting</item>
    ///   <item><see cref="ISecretResolver"/> — for Key Vault credential resolution</item>
    ///   <item><see cref="Core.Repositories.ITestResultRepository"/> — for Cosmos DB persistence</item>
    /// </list>
    /// </para>
    /// </summary>
    public static IServiceCollection AddReportWriter(this IServiceCollection services)
    {
        services.AddSingleton<IReportWriter, ReportWriter>();

        return services;
    }
}
