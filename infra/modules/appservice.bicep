// App Service module — Testurio
// Provisions an App Service plan and a Web App for the ASP.NET Core API.
// .NET 10 runtime, HTTPS-only enforced, system-assigned Managed Identity enabled.

param location string = resourceGroup().location
param planName string
param appName string
param skuName string = 'B1'
param appInsightsConnectionString string
param keyVaultUri string
param keyVaultName string

resource appServicePlan 'Microsoft.Web/serverfarms@2023-01-01' = {
  name: planName
  location: location
  sku: {
    name: skuName
  }
  properties: {
    reserved: false // Windows plan for .NET
  }
}

resource webApp 'Microsoft.Web/sites@2023-01-01' = {
  name: appName
  location: location
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    siteConfig: {
      netFrameworkVersion: 'v10.0'
      minTlsVersion: '1.2'
      ftpsState: 'Disabled'
      appSettings: [
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: appInsightsConnectionString
        }
        {
          name: 'AZURE_KEY_VAULT_URI'
          value: keyVaultUri
        }
        {
          name: 'DOTNET_ENVIRONMENT'
          value: 'Production'
        }
        {
          name: 'WEBSITE_RUN_FROM_PACKAGE'
          value: '1'
        }
        {
          name: 'Stripe__SecretKey'
          value: '@Microsoft.KeyVault(VaultName=${keyVaultName};SecretName=stripe-secret-key)'
        }
        {
          name: 'Stripe__WebhookSecret'
          value: '@Microsoft.KeyVault(VaultName=${keyVaultName};SecretName=stripe-webhook-secret)'
        }
        {
          name: 'Cosmos__ConnectionString'
          value: '@Microsoft.KeyVault(VaultName=${keyVaultName};SecretName=cosmos-connection-string)'
        }
        {
          name: 'ServiceBus__ConnectionString'
          value: '@Microsoft.KeyVault(VaultName=${keyVaultName};SecretName=servicebus-connection-string)'
        }
        {
          name: 'AzureAdB2C__ClientSecret'
          value: '@Microsoft.KeyVault(VaultName=${keyVaultName};SecretName=adb2c-client-secret)'
        }
      ]
    }
  }
}

// ─── Outputs ─────────────────────────────────────────────────────────────────

output appServiceName string = webApp.name
output principalId string = webApp.identity.principalId
output defaultHostname string = webApp.properties.defaultHostName
