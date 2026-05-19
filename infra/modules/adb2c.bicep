// Azure AD B2C module — Testurio
// Reference-only module: no resources are created here.
// AD B2C tenants are provisioned manually (they require a separate Azure AD tenant).
// This module accepts the tenant domain and client ID as inputs and passes them
// through as outputs so main.bicep can wire them into App Service and Container Apps
// configuration without repeating the values at the call site.

param tenantDomain string
param clientId string

// ─── Outputs ─────────────────────────────────────────────────────────────────

output tenantDomain string = tenantDomain
output clientId string = clientId
output authority string = 'https://${tenantDomain}/${tenantDomain}/v2.0/'
