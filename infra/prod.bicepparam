// prod.bicepparam — Testurio prod environment parameter file
// Production-tier overrides: P1v3 App Service, Standard ACR, standard AI Search,
// Standard SWA, Developer APIM.

using './main.bicep'

param environment = 'prod'
param prefix = 'testurio'
param adb2cTenantDomain = '__REPLACE_B2C_TENANT_DOMAIN__'
param adb2cClientId = '__REPLACE_B2C_CLIENT_ID__'
param apimPublisherEmail = '__REPLACE_APIM_PUBLISHER_EMAIL__'
