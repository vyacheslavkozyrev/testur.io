// Cosmos DB module — Testurio
// Provisions the account, database, and all containers with their indexing policies.
// Feature 0031: adds composite index on (workItemId, testType, source) to TestMemory container
//               to support efficient upsert-key lookup in UpsertFeedbackAsync.

param location string = resourceGroup().location
param accountName string
param databaseName string = 'testurio'
param serverless bool = false

resource cosmosAccount 'Microsoft.DocumentDB/databaseAccounts@2023-04-15' = {
  name: accountName
  location: location
  kind: 'GlobalDocumentDB'
  properties: {
    databaseAccountOfferType: 'Standard'
    consistencyPolicy: {
      defaultConsistencyLevel: 'Session'
    }
    locations: [
      {
        locationName: location
        failoverPriority: 0
      }
    ]
    capabilities: serverless ? [{ name: 'EnableServerless' }] : []
  }
}

resource database 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases@2023-04-15' = {
  parent: cosmosAccount
  name: databaseName
  properties: {
    resource: {
      id: databaseName
    }
  }
}

// ─── TestMemory container ────────────────────────────────────────────────────
// Partition key: /userId
// DiskANN vector index on /storyEmbedding (cosine distance, 1536 dimensions).
// Feature 0031: composite index on (workItemId, testType, source) for efficient
//               upsert-key lookup scoped to the userId partition.

// ─── Outputs ─────────────────────────────────────────────────────────────────

output cosmosAccountName string = cosmosAccount.name
output cosmosEndpoint string = cosmosAccount.properties.documentEndpoint

// ─────────────────────────────────────────────────────────────────────────────

resource testMemoryContainer 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2023-04-15' = {
  parent: database
  name: 'TestMemory'
  properties: {
    resource: {
      id: 'TestMemory'
      partitionKey: {
        paths: [
          '/userId'
        ]
        kind: 'Hash'
      }
      indexingPolicy: {
        indexingMode: 'consistent'
        automatic: true
        includedPaths: [
          {
            path: '/*'
          }
        ]
        excludedPaths: [
          {
            // Exclude the large embedding vector from the standard B-tree index —
            // it is indexed separately by the DiskANN vector index below.
            path: '/storyEmbedding/*'
          }
        ]
        // Feature 0031: composite index used by the upsert-key lookup query in
        // TestMemoryRepository.UpsertFeedbackAsync (workItemId + testType + source = 'qalead').
        // The composite path order must match the WHERE clause predicate order.
        compositeIndexes: [
          [
            {
              path: '/workItemId'
              order: 'ascending'
            }
            {
              path: '/testType'
              order: 'ascending'
            }
            {
              path: '/source'
              order: 'ascending'
            }
          ]
        ]
        vectorIndexes: [
          {
            path: '/storyEmbedding'
            type: 'diskANN'
          }
        ]
      }
      vectorEmbeddingPolicy: {
        vectorEmbeddings: [
          {
            path: '/storyEmbedding'
            dataType: 'float32'
            dimensions: 1536
            distanceFunction: 'cosine'
          }
        ]
      }
    }
  }
}
