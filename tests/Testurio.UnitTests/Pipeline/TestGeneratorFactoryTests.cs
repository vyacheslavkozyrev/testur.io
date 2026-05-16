using Microsoft.Extensions.DependencyInjection;
using Moq;
using Testurio.Core.Enums;
using Testurio.Core.Interfaces;
using Testurio.Pipeline.AgentRouter;

namespace Testurio.UnitTests.Pipeline;

public class TestGeneratorFactoryTests
{
    /// <summary>
    /// Builds a ServiceProvider with keyed <see cref="ITestGeneratorAgent"/> registrations
    /// so that <see cref="TestGeneratorFactory"/> can resolve them by key.
    /// </summary>
    private static (TestGeneratorFactory Factory, Mock<ITestGeneratorAgent> ApiMock, Mock<ITestGeneratorAgent> UiMock)
        CreateSutWithMocks()
    {
        var apiMock = new Mock<ITestGeneratorAgent>();
        var uiMock = new Mock<ITestGeneratorAgent>();

        var services = new ServiceCollection();
        services.AddKeyedSingleton<ITestGeneratorAgent>(TestGeneratorFactory.ApiKey, (_, _) => apiMock.Object);
        services.AddKeyedSingleton<ITestGeneratorAgent>(TestGeneratorFactory.UiE2eKey, (_, _) => uiMock.Object);

        var sp = services.BuildServiceProvider();
        var factory = new TestGeneratorFactory(sp);

        return (factory, apiMock, uiMock);
    }

    // ─── api type ─────────────────────────────────────────────────────────────

    [Fact]
    public void Create_ApiType_ReturnsApiTestGeneratorAgent()
    {
        var (factory, apiMock, _) = CreateSutWithMocks();

        var agent = factory.Create(TestType.Api);

        Assert.Same(apiMock.Object, agent);
    }

    // ─── ui_e2e type ──────────────────────────────────────────────────────────

    [Fact]
    public void Create_UiE2eType_ReturnsUiE2eTestGeneratorAgent()
    {
        var (factory, _, uiMock) = CreateSutWithMocks();

        var agent = factory.Create(TestType.UiE2e);

        Assert.Same(uiMock.Object, agent);
    }

    // ─── unknown type throws ─────────────────────────────────────────────────

    [Fact]
    public void Create_UnknownType_ThrowsArgumentOutOfRangeException()
    {
        var (factory, _, _) = CreateSutWithMocks();

        // Cast an out-of-range integer to TestType to simulate an unrecognised value.
        var unknownType = (TestType)999;

        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => factory.Create(unknownType));
        Assert.Equal("testType", ex.ParamName);
    }
}
