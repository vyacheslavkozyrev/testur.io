// Key Vault module — Testurio
// RBAC access model (no access policies), soft-delete 90 days, purge protection enabled.

param location string = resourceGroup().location
param vaultName string

resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: vaultName
  location: location
  properties: {
    sku: {
      family: 'A'
      name: 'standard'
    }
    tenantId: subscription().tenantId
    enableRbacAuthorization: true
    enableSoftDelete: true
    softDeleteRetentionInDays: 90
    enablePurgeProtection: true
  }
}

// ─── Outputs ─────────────────────────────────────────────────────────────────

output vaultName string = keyVault.name
output vaultUri string = keyVault.properties.vaultUri
