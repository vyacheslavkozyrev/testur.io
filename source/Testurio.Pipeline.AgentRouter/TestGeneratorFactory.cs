using Microsoft.Extensions.DependencyInjection;
using Testurio.Core.Enums;
using Testurio.Core.Interfaces;

namespace Testurio.Pipeline.AgentRouter;

/// <summary>
/// Creates <see cref="ITestGeneratorAgent"/> instances by test type using keyed DI.
/// Concrete implementations (<c>ApiTestGeneratorAgent</c>, <c>UiE2eTestGeneratorAgent</c>)
/// are registered in feature 0028 (Testurio.Pipeline.Generators) using the DI service keys
/// <c>"api"</c> and <c>"ui_e2e"</c> respectively.
/// </summary>
public sealed class TestGeneratorFactory : ITestGeneratorFactory
{
    /// <summary>DI service key for the API test generator.</summary>
    public const string ApiKey = "api";

    /// <summary>DI service key for the UI end-to-end test generator.</summary>
    public const string UiE2eKey = "ui_e2e";

    private readonly IServiceProvider _serviceProvider;

    public TestGeneratorFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="testType"/> is not a recognised MVP value.
    /// </exception>
    public ITestGeneratorAgent Create(TestType testType)
    {
        var key = testType switch
        {
            TestType.Api   => ApiKey,
            TestType.UiE2e => UiE2eKey,
            _              => throw new ArgumentOutOfRangeException(
                                  nameof(testType),
                                  testType,
                                  $"Unrecognised test type '{testType}'. Valid values are: {TestType.Api}, {TestType.UiE2e}.")
        };

        return _serviceProvider.GetRequiredKeyedService<ITestGeneratorAgent>(key);
    }
}
