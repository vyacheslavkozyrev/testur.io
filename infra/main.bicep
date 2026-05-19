// main.bicep — Testurio root orchestrator
// Orchestrates all infrastructure modules with environment-driven SKU variables.
// Parameters: environment ('dev' | 'prod'), prefix (resource name prefix).

@allowed(['dev', 'prod'])
param environment string = 'dev'

param prefix string = 'testurio'
param location string = resourceGroup().location

// AD B2C reference params (provisioned out-of-band)
param adb2cTenantDomain string
param adb2cClientId string

// APIM publisher email (required by the APIM resource)
param apimPublisherEmail string

// ─── Environment-driven SKU variables ────────────────────────────────────────

var isProd = environment == 'prod'

var appServiceSkuName   = isProd ? 'P1v3'    : 'B1'
var acrSkuName          = isProd ? 'Standard' : 'Basic'
var searchSkuName       = isProd ? 'standard' : 'free'
var swaSkuName          = isProd ? 'Standard' : 'Free'

// Key Vault Secrets User built-in role ID
var keyVaultSecretsUserRoleId = '4633458b-17de-408a-b874-0445c86b69e6'

// ─── Key Vault ────────────────────────────────────────────────────────────────

module keyVault 'modules/keyvault.bicep' = {
  name: 'keyVault'
  params: {
    location: location
    vaultName: '${prefix}-kv-${environment}'
  }
}

// ─── Application Insights ────────────────────────────────────────────────────

module appInsights 'modules/appinsights.bicep' = {
  name: 'appInsights'
  params: {
    location: location
    workspaceName: '${prefix}-log-${environment}'
    appInsightsName: '${prefix}-ai-${environment}'
  }
}

// ─── Container Registry ──────────────────────────────────────────────────────

module acr 'modules/acr.bicep' = {
  name: 'acr'
  params: {
    location: location
    registryName: '${prefix}acr${environment}'
    skuName: acrSkuName
  }
}

// ─── NAT Gateway ─────────────────────────────────────────────────────────────

module natGateway 'modules/natgateway.bicep' = {
  name: 'natGateway'
  params: {
    location: location
    natGatewayName: '${prefix}-nat-${environment}'
    publicIpPrefixName: '${prefix}-nat-prefix-${environment}'
  }
}

// ─── Static Web Apps ─────────────────────────────────────────────────────────

module staticWebApp 'modules/staticwebapp.bicep' = {
  name: 'staticWebApp'
  params: {
    location: location
    appName: '${prefix}-web-${environment}'
    skuName: swaSkuName
  }
}

// ─── App Service ─────────────────────────────────────────────────────────────

module appService 'modules/appservice.bicep' = {
  name: 'appService'
  params: {
    location: location
    planName: '${prefix}-plan-${environment}'
    appName: '${prefix}-api-${environment}'
    skuName: appServiceSkuName
    appInsightsConnectionString: appInsights.outputs.connectionString
    keyVaultUri: keyVault.outputs.vaultUri
  }
}

// ─── Container Apps ───────────────────────────────────────────────────────────

module containerApps 'modules/containerapps.bicep' = {
  name: 'containerApps'
  params: {
    location: location
    environmentName: '${prefix}-cae-${environment}'
    containerAppName: '${prefix}-worker-${environment}'
    acrLoginServer: acr.outputs.loginServer
    appInsightsConnectionString: appInsights.outputs.connectionString
    keyVaultUri: keyVault.outputs.vaultUri
    serviceBusConnectionSecretUri: '${keyVault.outputs.vaultUri}secrets/servicebus-connection'
    cosmosConnectionSecretUri: '${keyVault.outputs.vaultUri}secrets/cosmos-connection'
    anthropicApiKeySecretUri: '${keyVault.outputs.vaultUri}secrets/anthropic-api-key'
  }
}

// ─── AI Search ────────────────────────────────────────────────────────────────

module aiSearch 'modules/aisearch.bicep' = {
  name: 'aiSearch'
  params: {
    location: location
    searchServiceName: '${prefix}-search-${environment}'
    skuName: searchSkuName
  }
}

// ─── Front Door ───────────────────────────────────────────────────────────────

module frontDoor 'modules/frontdoor.bicep' = {
  name: 'frontDoor'
  params: {
    profileName: '${prefix}-fd-${environment}'
    swaHostname: staticWebApp.outputs.defaultHostname
    apiHostname: appService.outputs.defaultHostname
  }
}

// ─── API Management ───────────────────────────────────────────────────────────

module apim 'modules/apim.bicep' = {
  name: 'apim'
  params: {
    location: location
    serviceName: '${prefix}-apim-${environment}'
    publisherEmail: apimPublisherEmail
    environment: environment
  }
}

// ─── AD B2C reference ─────────────────────────────────────────────────────────

module adb2c 'modules/adb2c.bicep' = {
  name: 'adb2c'
  params: {
    tenantDomain: adb2cTenantDomain
    clientId: adb2cClientId
  }
}

// ─── Managed Identity role assignments ───────────────────────────────────────
// Grant 'Key Vault Secrets User' to App Service and Container Apps system identities.

resource appServiceKvRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(keyVault.name, appService.outputs.principalId, keyVaultSecretsUserRoleId)
  scope: resourceGroup()
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', keyVaultSecretsUserRoleId)
    principalId: appService.outputs.principalId
    principalType: 'ServicePrincipal'
  }
}

resource workerKvRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(keyVault.name, containerApps.outputs.principalId, keyVaultSecretsUserRoleId)
  scope: resourceGroup()
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', keyVaultSecretsUserRoleId)
    principalId: containerApps.outputs.principalId
    principalType: 'ServicePrincipal'
  }
}

// Grant ACR Pull to Container Apps identity so it can pull images without admin credentials.
var acrPullRoleId = '7f951dda-4ed3-4680-a7ca-43fe172d538d'

resource workerAcrRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(acr.outputs.resourceId, containerApps.outputs.principalId, acrPullRoleId)
  scope: resourceGroup()
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', acrPullRoleId)
    principalId: containerApps.outputs.principalId
    principalType: 'ServicePrincipal'
  }
}

// ─── Outputs ─────────────────────────────────────────────────────────────────

output keyVaultName string = keyVault.outputs.vaultName
output keyVaultUri string = keyVault.outputs.vaultUri
output appInsightsConnectionString string = appInsights.outputs.connectionString
output acrLoginServer string = acr.outputs.loginServer
output staticWebAppDefaultHostname string = staticWebApp.outputs.defaultHostname
output staticWebAppDeploymentTokenSecretName string = staticWebApp.outputs.deploymentTokenSecretName
output apiDefaultHostname string = appService.outputs.defaultHostname
output appServiceName string = appService.outputs.appServiceName
output workerContainerAppName string = containerApps.outputs.containerAppName
output searchEndpoint string = aiSearch.outputs.searchEndpoint
output frontDoorHostname string = frontDoor.outputs.frontDoorEndpointHostname
output apimGatewayUrl string = apim.outputs.gatewayUrl
output b2cAuthority string = adb2c.outputs.authority
output natGatewayEgressIps array = natGateway.outputs.publicIpAddresses
