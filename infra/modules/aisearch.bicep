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

// ─── TestMemory vector index ─────────────────────────────────────────────────
// HNSW algorithm, cosine distance, 1536 dimensions (text-embedding-3-small).
// Filtered by userId and testType; isDeleted excluded from results by the query layer.

resource testMemoryIndex 'Microsoft.Search/searchServices/indexes@2023-11-01' = {
  parent: searchService
  name: 'test-memory'
  properties: {
    fields: [
      { name: 'id',             type: 'Edm.String',              key: true,  searchable: false, filterable: false, retrievable: true  }
      { name: 'userId',         type: 'Edm.String',              key: false, searchable: false, filterable: true,  retrievable: true  }
      { name: 'projectId',      type: 'Edm.String',              key: false, searchable: false, filterable: true,  retrievable: true  }
      { name: 'testType',       type: 'Edm.String',              key: false, searchable: false, filterable: true,  retrievable: true  }
      { name: 'storyText',      type: 'Edm.String',              key: false, searchable: true,  filterable: false, retrievable: true  }
      { name: 'scenarioText',   type: 'Edm.String',              key: false, searchable: false, filterable: false, retrievable: true  }
      { name: 'passRate',       type: 'Edm.Double',              key: false, searchable: false, filterable: true,  retrievable: true  }
      { name: 'runCount',       type: 'Edm.Int32',               key: false, searchable: false, filterable: true,  retrievable: true  }
      { name: 'isDeleted',      type: 'Edm.Boolean',             key: false, searchable: false, filterable: true,  retrievable: true  }
      {
        name: 'storyEmbedding'
        type: 'Collection(Edm.Single)'
        searchable: true
        retrievable: false
        dimensions: 1536
        vectorSearchProfile: 'default-profile'
      }
    ]
    vectorSearch: {
      algorithms: [
        {
          name: 'hnsw-cosine'
          kind: 'hnsw'
          hnswParameters: {
            metric: 'cosine'
            m: 4
            efConstruction: 400
            efSearch: 500
          }
        }
      ]
      profiles: [
        {
          name: 'default-profile'
          algorithmConfigurationName: 'hnsw-cosine'
        }
      ]
    }
    semantic: {
      configurations: [
        {
          name: 'default'
          prioritizedFields: {
            contentFields: [
              { fieldName: 'storyText' }
            ]
          }
        }
      ]
    }
  }
}

// ─── Outputs ─────────────────────────────────────────────────────────────────

output searchEndpoint string = 'https://${searchService.name}.search.windows.net'
output resourceId string = searchService.id
