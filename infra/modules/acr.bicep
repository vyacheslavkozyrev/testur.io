// Container Registry module — Testurio
// Admin account disabled — access is granted via Managed Identity role assignments.

param location string = resourceGroup().location
param registryName string
param skuName string = 'Basic'

resource containerRegistry 'Microsoft.ContainerRegistry/registries@2023-07-01' = {
  name: registryName
  location: location
  sku: {
    name: skuName
  }
  properties: {
    adminUserEnabled: false
  }
}

// ─── Outputs ─────────────────────────────────────────────────────────────────

output loginServer string = containerRegistry.properties.loginServer
output resourceId string = containerRegistry.id
