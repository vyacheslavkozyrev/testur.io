using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Playwright;
using Moq;
using Testurio.Core.Exceptions;
using Testurio.Core.Interfaces;
using Testurio.Pipeline.Executors;

namespace Testurio.UnitTests.Pipeline.Executors;

/// <summary>
/// Unit tests for <see cref="PlaywrightExecutor.ApplyPageTimeout"/> — covers the
/// per-action timeout configuration introduced in feature 0022.
/// </summary>
public class PlaywrightExecutorTests
{
    // ─── ApplyPageTimeout ─────────────────────────────────────────────────────

    [Fact]
    public void ApplyPageTimeout_CallsSetDefaultTimeout_WithConvertedMilliseconds()
    {
        var pageMock = new Mock<IPage>(MockBehavior.Loose);

        PlaywrightExecutor.ApplyPageTimeout(pageMock.Object, timeoutSeconds: 30);

        // 30 seconds × 1000 = 30000 ms
        pageMock.Verify(p => p.SetDefaultTimeout(30_000f), Times.Once);
    }

    [Fact]
    public void ApplyPageTimeout_ConvertsSecondsToMilliseconds_Correctly()
    {
        var pageMock = new Mock<IPage>(MockBehavior.Loose);

        PlaywrightExecutor.ApplyPageTimeout(pageMock.Object, timeoutSeconds: 5);

        pageMock.Verify(p => p.SetDefaultTimeout(5_000f), Times.Once);
    }

    [Fact]
    public void ApplyPageTimeout_HandlesMaximumTimeout_Correctly()
    {
        var pageMock = new Mock<IPage>(MockBehavior.Loose);

        PlaywrightExecutor.ApplyPageTimeout(pageMock.Object, timeoutSeconds: 120);

        pageMock.Verify(p => p.SetDefaultTimeout(120_000f), Times.Once);
    }

    [Fact]
    public void ApplyPageTimeout_CallsSetDefaultTimeout_ExactlyOnce()
    {
        var pageMock = new Mock<IPage>(MockBehavior.Loose);

        PlaywrightExecutor.ApplyPageTimeout(pageMock.Object, timeoutSeconds: 45);

        // Ensure the method is called once and only once — not per-action.
        pageMock.Verify(p => p.SetDefaultTimeout(It.IsAny<float>()), Times.Once);
    }

    // ─── BuildContextOptionsAsync / credential injection (existing behaviour) ─

    [Fact]
    public async Task BuildContextOptionsAsync_ReturnsIpAllowlistOptions_WhenNoCredentialsNeeded()
    {
        var credentialProviderMock = new Mock<IProjectAccessCredentialProvider>();
        credentialProviderMock
            .Setup(p => p.ResolveAsync(It.IsAny<Testurio.Core.Entities.Project>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Testurio.Core.Models.ProjectAccessCredentials.IpAllowlist());

        var executor = new PlaywrightExecutor(
            credentialProviderMock.Object,
            NullLogger<PlaywrightExecutor>.Instance);

        var project = new Testurio.Core.Entities.Project
        {
            UserId = "user-1",
            Name = "Test Project",
            ProductUrl = "https://staging.example.com",
            TestingStrategy = "Smoke tests.",
            RequestTimeoutSeconds = 30,
        };

        var options = await executor.BuildContextOptionsAsync(project, CancellationToken.None);

        Assert.Null(options.HttpCredentials);
        Assert.Null(options.ExtraHTTPHeaders);
    }
}
