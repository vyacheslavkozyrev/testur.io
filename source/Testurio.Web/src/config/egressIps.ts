// Testurio NAT Gateway egress IPs — clients add these to their staging environment firewall.
// Update this list whenever the NAT Gateway configuration changes.
export const PUBLISHED_EGRESS_IPS = ['20.0.0.1', '20.0.0.2', '20.0.0.3'] as const;
