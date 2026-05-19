// Container Apps module — Testurio
// Provisions a Container Apps environment and the Worker Container App.
// ACR image reference, Key Vault reference env vars, system-assigned Managed Identity.

param location string = resourceGroup().location
param environmentName string
param containerAppName string
param acrLoginServer string
param workerImageTag string = 'latest'
param appInsightsConnectionString string
param keyVaultUri string
param serviceBusConnectionSecretUri string
param cosmosConnectionSecretUri string
param anthropicApiKeySecretUri string

resource containerAppsEnvironment 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: environmentName
  location: location
  properties: {
    appLogsConfiguration: {
      destination: 'azure-monitor'
    }
  }
}

resource workerApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: containerAppName
  location: location
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    managedEnvironmentId: containerAppsEnvironment.id
    configuration: {
      activeRevisionsMode: 'Single'
      registries: [
        {
          server: acrLoginServer
          identity: 'system'
        }
      ]
      secrets: [
        {
          name: 'servicebus-connection'
          keyVaultUrl: serviceBusConnectionSecretUri
          identity: 'system'
        }
        {
          name: 'cosmos-connection'
          keyVaultUrl: cosmosConnectionSecretUri
          identity: 'system'
        }
        {
          name: 'anthropic-api-key'
          keyVaultUrl: anthropicApiKeySecretUri
          identity: 'system'
        }
      ]
    }
    template: {
      containers: [
        {
          name: 'worker'
          image: '${acrLoginServer}/testurio-worker:${workerImageTag}'
          resources: {
            cpu: json('0.5')
            memory: '1Gi'
          }
          env: [
            {
              name: 'DOTNET_ENVIRONMENT'
              value: 'Production'
            }
            {
              name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
              value: appInsightsConnectionString
            }
            {
              name: 'AZURE_KEY_VAULT_URI'
              value: keyVaultUri
            }
            {
              name: 'ServiceBus__ConnectionString'
              secretRef: 'servicebus-connection'
            }
            {
              name: 'Cosmos__ConnectionString'
              secretRef: 'cosmos-connection'
            }
            {
              name: 'Anthropic__ApiKey'
              secretRef: 'anthropic-api-key'
            }
          ]
        }
      ]
      scale: {
        minReplicas: 0
        maxReplicas: 10
      }
    }
  }
}

// ─── Outputs ─────────────────────────────────────────────────────────────────

output containerAppName string = workerApp.name
output principalId string = workerApp.identity.principalId
