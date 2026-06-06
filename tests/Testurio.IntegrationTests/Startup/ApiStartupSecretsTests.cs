using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testurio.Core.Interfaces;
using Testurio.Infrastructure;
using Testurio.Infrastructure.Cosmos;
using Testurio.Infrastructure.KeyVault;
using Testurio.Infrastructure.Options;
using Testurio.Infrastructure.Seeding;

namespace Testurio.IntegrationTests.Startup;

/// <summary>
/// Integration test verifying that <c>Testurio.Api</c> starts cleanly with
/// <see cref="NullKeyVaultSecretLoader"/> and local configuration values,
/// and that all previously registered <see cref="Microsoft.Extensions.Options.IOptions{T}"/>
/// instances still pass <c>ValidateOnStart</c> validation.
/// </summary>
public class ApiStartupSecretsTests : IClassFixture<ApiStartupSecretsTests.Factory>
{
    private readonly Factory _factory;

    public ApiStartupSecretsTests(Factory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ApiStartsCleanly_WithNullKeyVaultLoaderAndLocalConfig()
    {
        // If the factory starts without throwing, all IOptions<T> validation passed.
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/health-check-nonexistent");
        // Any response (404 included) means the host started successfully.
        Assert.NotNull(response);
    }

    [Fact]
    public void InfrastructureSecrets_AreRegistered_AndPopulatedFromLocalConfig()
    {
        using var scope = _factory.Services.CreateScope();
        var secrets = scope.ServiceProvider.GetRequiredService<InfrastructureSecrets>();
        Assert.NotNull(secrets);
        // Verify the values came from the in-memory test config (not empty / null)
        Assert.NotEmpty(secrets.CosmosConnectionString);
        Assert.NotEmpty(secrets.ServiceBusConnectionString);
        Assert.NotEmpty(secrets.BlobStorageConnectionString);
    }

    [Fact]
    public void AnthropicSecrets_AreRegistered()
    {
        using var scope = _factory.Services.CreateScope();
        var secrets = scope.ServiceProvider.GetRequiredService<AnthropicSecrets>();
        Assert.NotNull(secrets);
    }

    [Fact]
    public void StripeSecrets_AreRegistered_AndPopulatedFromLocalConfig()
    {
        using var scope = _factory.Services.CreateScope();
        var secrets = scope.ServiceProvider.GetRequiredService<StripeSecrets>();
        Assert.NotNull(secrets);
        Assert.NotEmpty(secrets.SecretKey);
        Assert.NotEmpty(secrets.WebhookSecret);
    }

    [Fact]
    public void KeyVaultSecretLoader_IsNullLoader_InDevelopmentEnvironment()
    {
        using var scope = _factory.Services.CreateScope();
        var loader = scope.ServiceProvider.GetRequiredService<IKeyVaultSecretLoader>();
        Assert.IsType<NullKeyVaultSecretLoader>(loader);
    }

    public sealed class Factory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");

            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Infrastructure:CosmosConnectionString"] = "AccountEndpoint=https://localhost:8081/;AccountKey=AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA==",
                    ["Infrastructure:CosmosDatabaseName"] = "TestDb",
                    ["Infrastructure:ServiceBusConnectionString"] = "Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=dummykey==",
                    ["Infrastructure:TestRunJobQueueName"] = "test-runs",
                    ["Infrastructure:BlobStorageConnectionString"] = "UseDevelopmentStorage=true",
                    ["Infrastructure:ExecutionLogsBlobContainerName"] = "execution-logs",
                    ["Infrastructure:ReportTemplatesBlobContainerName"] = "report-templates",
                    ["Infrastructure:ReportsBlobContainerName"] = "reports",
                    ["Stripe:SecretKey"] = "sk_test_startup",
                    ["Stripe:WebhookSecret"] = "whsec_startup",
                    ["Stripe:PriceIds:TestPro_Monthly"] = "price_test",
                    ["AzureAdB2C:Authority"] = "https://login.microsoftonline.com/test-tenant",
                    ["AzureAdB2C:ClientId"] = "test-client-id",
                    ["App:BaseUrl"] = "https://localhost",
                    ["Claude:ModelId"] = "claude-opus-4-7",
                });
            });

            builder.ConfigureTestServices(services =>
            {
                services.Replace(ServiceDescriptor.Singleton<ISecretResolver>(_ => new PassthroughSecretResolver()));
                services.Replace(ServiceDescriptor.Singleton<ICosmosDbInitializer>(_ => new NoOpCosmosDbInitializer()));
                services.Replace(ServiceDescriptor.Singleton<IPromptTemplateSeeder>(_ => new NoOpPromptTemplateSeeder()));
                services.Replace(ServiceDescriptor.Singleton<IPlanSeeder>(_ => new NoOpPlanSeeder()));
            });
        }
    }
}
