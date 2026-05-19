// Static Web Apps module — Testurio
// Hosts the Next.js public site and user portal on Azure Static Web Apps.

param location string = resourceGroup().location
param appName string
param skuName string = 'Free'

resource staticWebApp 'Microsoft.Web/staticSites@2023-01-01' = {
  name: appName
  location: location
  sku: {
    name: skuName
    tier: skuName
  }
  properties: {}
}

// Store the deployment token in Key Vault so CI/CD can retrieve it at runtime
// without embedding it in workflow YAML. The secret name is an output so
// main.bicep can create the Key Vault secret entry.
var deploymentTokenSecretName = 'swa-deployment-token'

// ─── Outputs ─────────────────────────────────────────────────────────────────

output defaultHostname string = staticWebApp.properties.defaultHostname
output deploymentTokenSecretName string = deploymentTokenSecretName
output staticWebAppName string = staticWebApp.name
