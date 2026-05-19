// NAT Gateway module — Testurio
// Provisions a public IP prefix, a NAT Gateway resource, and accepts a subnet resource ID
// for association. All worker container egress routes through this gateway, giving Testurio
// a fixed, publishable set of egress IPs for client firewall allowlisting.

param location string = resourceGroup().location
param natGatewayName string
param publicIpPrefixName string
param prefixLength int = 29

resource publicIpPrefix 'Microsoft.Network/publicIPPrefixes@2023-09-01' = {
  name: publicIpPrefixName
  location: location
  sku: {
    name: 'Standard'
    tier: 'Regional'
  }
  properties: {
    prefixLength: prefixLength
    publicIPAddressVersion: 'IPv4'
  }
}

resource natGateway 'Microsoft.Network/natGateways@2023-09-01' = {
  name: natGatewayName
  location: location
  sku: {
    name: 'Standard'
  }
  properties: {
    publicIpPrefixes: [
      {
        id: publicIpPrefix.id
      }
    ]
    idleTimeoutInMinutes: 4
  }
}

// ─── Outputs ─────────────────────────────────────────────────────────────────

// Returns the allocated public IP prefix CIDR — used by operators to publish
// the fixed egress IP range in documentation for client firewall allowlisting.
output publicIpAddresses array = [publicIpPrefix.properties.ipPrefix]
output natGatewayId string = natGateway.id
