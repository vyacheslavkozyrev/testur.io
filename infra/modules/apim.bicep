// API Management module — Testurio
// Consumption tier for dev (cost-optimised, no VNet), Developer tier for prod.
// Used for webhook auth and rate limiting on /webhooks/ado and /webhooks/jira.

param location string = resourceGroup().location
param serviceName string
param publisherEmail string
param publisherName string = 'Testurio'
param environment string = 'dev'

var skuName = environment == 'prod' ? 'Developer' : 'Consumption'
var skuCapacity = environment == 'prod' ? 1 : 0 // Consumption has no capacity

resource apim 'Microsoft.ApiManagement/service@2022-08-01' = {
  name: serviceName
  location: location
  sku: {
    name: skuName
    capacity: skuCapacity
  }
  properties: {
    publisherEmail: publisherEmail
    publisherName: publisherName
  }
}

// ─── Outputs ─────────────────────────────────────────────────────────────────

output gatewayUrl string = apim.properties.gatewayUrl
