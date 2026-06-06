using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using Testurio.Core.Interfaces;
using Testurio.Infrastructure;
using Testurio.Infrastructure.KeyVault;
using Testurio.Infrastructure.Options;

namespace Testurio.UnitTests.Infrastructure;

/// <summary>
/// Unit tests for the <c>Add*Secrets</c> DI extension methods.
/// Verifies that in development mode each helper reads from <see cref="IConfiguration"/>
/// and registers the correct singleton; in production mode it reads from
/// <see cref="IKeyVaultSecretLoader"/>.
/// </summary>
public class SecretsRegistrationTests
{
    // ─── helpers ──────────────────────────────────────────────────────────────

    private static IConfiguration BuildConfig(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    private static IHostEnvironment DevEnvironment()
    {
        var env = new Mock<IHostEnvironment>();
        env.Setup(e => e.EnvironmentName).Returns("Development");
        return env.Object;
    }

    private static IHostEnvironment ProdEnvironment()
    {
        var env = new Mock<IHostEnvironment>();
        env.Setup(e => e.EnvironmentName).Returns("Production");
        return env.Object;
    }

    // ─── AddInfrastructureSecretsAsync ────────────────────────────────────────

    [Fact]
    public async Task AddInfrastructureSecretsAsync_Dev_ReadsFromConfiguration()
    {
        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["Infrastructure:CosmosConnectionString"] = "cosmos-dev",
            ["Infrastructure:ServiceBusConnectionString"] = "sb-dev",
            ["Infrastructure:BlobStorageConnectionString"] = "blob-dev",
        });

        var services = new ServiceCollection();
        await services.AddInfrastructureSecretsAsync(config, DevEnvironment());

        var sp = services.BuildServiceProvider();
        var secrets = sp.GetRequiredService<InfrastructureSecrets>();

        Assert.Equal("cosmos-dev", secrets.CosmosConnectionString);
        Assert.Equal("sb-dev", secrets.ServiceBusConnectionString);
        Assert.Equal("blob-dev", secrets.BlobStorageConnectionString);
    }

    [Fact]
    public async Task AddInfrastructureSecretsAsync_Dev_UsesMissingFieldsAsEmptyString()
    {
        var config = BuildConfig(new Dictionary<string, string?>());

        var services = new ServiceCollection();
        await services.AddInfrastructureSecretsAsync(config, DevEnvironment());

        var sp = services.BuildServiceProvider();
        var secrets = sp.GetRequiredService<InfrastructureSecrets>();

        Assert.Equal(string.Empty, secrets.CosmosConnectionString);
        Assert.Equal(string.Empty, secrets.ServiceBusConnectionString);
        Assert.Equal(string.Empty, secrets.BlobStorageConnectionString);
    }

    [Fact]
    public async Task AddInfrastructureSecretsAsync_Prod_ReadsFromKeyVaultLoader()
    {
        var loader = new Mock<IKeyVaultSecretLoader>();
        loader.Setup(l => l.GetSecretAsync("cosmos-connection-string", It.IsAny<CancellationToken>()))
              .ReturnsAsync("cosmos-prod");
        loader.Setup(l => l.GetSecretAsync("servicebus-connection-string", It.IsAny<CancellationToken>()))
              .ReturnsAsync("sb-prod");
        loader.Setup(l => l.GetSecretAsync("blob-storage-connection-string", It.IsAny<CancellationToken>()))
              .ReturnsAsync("blob-prod");

        var services = new ServiceCollection();
        services.AddSingleton(loader.Object);

        await services.AddInfrastructureSecretsAsync(BuildConfig([]), ProdEnvironment());

        var sp = services.BuildServiceProvider();
        var secrets = sp.GetRequiredService<InfrastructureSecrets>();

        Assert.Equal("cosmos-prod", secrets.CosmosConnectionString);
        Assert.Equal("sb-prod", secrets.ServiceBusConnectionString);
        Assert.Equal("blob-prod", secrets.BlobStorageConnectionString);
    }

    // ─── AddAnthropicSecretsAsync ─────────────────────────────────────────────

    [Fact]
    public async Task AddAnthropicSecretsAsync_Dev_ReadsFromConfiguration()
    {
        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["Claude:ApiKey"] = "sk-ant-dev",
        });

        var services = new ServiceCollection();
        await services.AddAnthropicSecretsAsync(config, DevEnvironment());

        var sp = services.BuildServiceProvider();
        var secrets = sp.GetRequiredService<AnthropicSecrets>();

        Assert.Equal("sk-ant-dev", secrets.ApiKey);
    }

    [Fact]
    public async Task AddAnthropicSecretsAsync_Prod_ReadsFromKeyVaultLoader()
    {
        var loader = new Mock<IKeyVaultSecretLoader>();
        loader.Setup(l => l.GetSecretAsync("anthropic-api-key", It.IsAny<CancellationToken>()))
              .ReturnsAsync("sk-ant-prod");

        var services = new ServiceCollection();
        services.AddSingleton(loader.Object);
        await services.AddAnthropicSecretsAsync(BuildConfig([]), ProdEnvironment());

        var sp = services.BuildServiceProvider();
        var secrets = sp.GetRequiredService<AnthropicSecrets>();

        Assert.Equal("sk-ant-prod", secrets.ApiKey);
    }

    // ─── AddAzureOpenAISecretsAsync ───────────────────────────────────────────

    [Fact]
    public async Task AddAzureOpenAISecretsAsync_Dev_ReadsFromConfiguration()
    {
        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["AzureOpenAI:ApiKey"] = "oai-dev",
        });

        var services = new ServiceCollection();
        await services.AddAzureOpenAISecretsAsync(config, DevEnvironment());

        var sp = services.BuildServiceProvider();
        var secrets = sp.GetRequiredService<AzureOpenAISecrets>();

        Assert.Equal("oai-dev", secrets.ApiKey);
    }

    [Fact]
    public async Task AddAzureOpenAISecretsAsync_Prod_ReadsFromKeyVaultLoader()
    {
        var loader = new Mock<IKeyVaultSecretLoader>();
        loader.Setup(l => l.GetSecretAsync("azure-openai-api-key", It.IsAny<CancellationToken>()))
              .ReturnsAsync("oai-prod");

        var services = new ServiceCollection();
        services.AddSingleton(loader.Object);
        await services.AddAzureOpenAISecretsAsync(BuildConfig([]), ProdEnvironment());

        var sp = services.BuildServiceProvider();
        var secrets = sp.GetRequiredService<AzureOpenAISecrets>();

        Assert.Equal("oai-prod", secrets.ApiKey);
    }

    // ─── AddStripeSecretsAsync ────────────────────────────────────────────────

    [Fact]
    public async Task AddStripeSecretsAsync_Dev_ReadsFromConfiguration()
    {
        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["Stripe:SecretKey"] = "sk_test_dev",
            ["Stripe:WebhookSecret"] = "whsec_dev",
        });

        var services = new ServiceCollection();
        await services.AddStripeSecretsAsync(config, DevEnvironment());

        var sp = services.BuildServiceProvider();
        var secrets = sp.GetRequiredService<StripeSecrets>();

        Assert.Equal("sk_test_dev", secrets.SecretKey);
        Assert.Equal("whsec_dev", secrets.WebhookSecret);
    }

    [Fact]
    public async Task AddStripeSecretsAsync_Prod_ReadsFromKeyVaultLoader()
    {
        var loader = new Mock<IKeyVaultSecretLoader>();
        loader.Setup(l => l.GetSecretAsync("stripe-secret-key", It.IsAny<CancellationToken>()))
              .ReturnsAsync("sk_live_prod");
        loader.Setup(l => l.GetSecretAsync("stripe-webhook-secret", It.IsAny<CancellationToken>()))
              .ReturnsAsync("whsec_prod");

        var services = new ServiceCollection();
        services.AddSingleton(loader.Object);
        await services.AddStripeSecretsAsync(BuildConfig([]), ProdEnvironment());

        var sp = services.BuildServiceProvider();
        var secrets = sp.GetRequiredService<StripeSecrets>();

        Assert.Equal("sk_live_prod", secrets.SecretKey);
        Assert.Equal("whsec_prod", secrets.WebhookSecret);
    }

    // ─── AddKeyVaultSecretLoader ──────────────────────────────────────────────

    [Fact]
    public void AddKeyVaultSecretLoader_Dev_RegistersNullLoader()
    {
        var services = new ServiceCollection();
        services.AddKeyVaultSecretLoader(BuildConfig([]), DevEnvironment());

        var sp = services.BuildServiceProvider();
        var loader = sp.GetRequiredService<IKeyVaultSecretLoader>();

        Assert.IsType<NullKeyVaultSecretLoader>(loader);
    }

    [Fact]
    public void AddKeyVaultSecretLoader_Prod_ThrowsWhenKeyVaultUriMissing()
    {
        var services = new ServiceCollection();
        Assert.Throws<InvalidOperationException>(() =>
            services.AddKeyVaultSecretLoader(BuildConfig([]), ProdEnvironment()));
    }
}
