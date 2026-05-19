// dev.bicepparam — Testurio dev environment parameter file
// Cost-optimised overrides: B1 App Service, Basic ACR, free AI Search, Free SWA, Consumption APIM.

using './main.bicep'

param environment = 'dev'
param prefix = 'testurio'
param adb2cTenantDomain = '__REPLACE_B2C_TENANT_DOMAIN__'
param adb2cClientId = '__REPLACE_B2C_CLIENT_ID__'
param apimPublisherEmail = '__REPLACE_APIM_PUBLISHER_EMAIL__'
