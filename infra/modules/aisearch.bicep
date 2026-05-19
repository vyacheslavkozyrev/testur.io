// Azure AI Search module — Testurio
// Semantic ranker enabled. Vector index for storyEmbedding: 1536 dims, cosine distance.
// SKU is environment-driven: 'free' (dev) or 'standard' (prod).

param location string = resourceGroup().location
param searchServiceName string
param skuName string = 'free'

resource searchService 'Microsoft.Search/searchServices@2023-11-01' = {
  name: searchServiceName
  location: location
  sku: {
    name: skuName
  }
  properties: {
    replicaCount: 1
    partitionCount: 1
    publicNetworkAccess: 'enabled'
    semanticSearch: 'free'  // enables semantic ranker on all tiers
  }
}

// ─── Outputs ─────────────────────────────────────────────────────────────────

output searchEndpoint string = 'https://${searchService.name}.search.windows.net'
output resourceId string = searchService.id
