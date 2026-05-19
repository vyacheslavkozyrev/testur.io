// Service Bus module — Testurio
// Provisions the namespace, the main test-run job queue, and the comment-events topic/subscription.
// Feature 0031: adds testurio-comment-events topic + worker subscription for FeedbackLoop processing.

param location string = resourceGroup().location
param namespaceName string
param testRunJobQueueName string = 'testurio-test-run-jobs'
param commentEventTopicName string = 'testurio-comment-events'
param commentEventSubscriptionName string = 'worker'

resource serviceBusNamespace 'Microsoft.ServiceBus/namespaces@2022-10-01-preview' = {
  name: namespaceName
  location: location
  sku: {
    name: 'Standard'
    tier: 'Standard'
  }
}

// ─── Test-run job queue (existing) ───────────────────────────────────────────
// Consumed by TestRunJobProcessor in Testurio.Worker.

resource testRunJobQueue 'Microsoft.ServiceBus/namespaces/queues@2022-10-01-preview' = {
  parent: serviceBusNamespace
  name: testRunJobQueueName
  properties: {
    maxDeliveryCount: 5
    lockDuration: 'PT5M'
    defaultMessageTimeToLive: 'P14D'
    deadLetteringOnMessageExpiration: true
  }
}

// ─── Comment events topic (feature 0031) ─────────────────────────────────────
// Testurio.Api publishes CommentWebhookEvent messages here from the ADO and Jira
// comment-created webhook handlers. Testurio.Worker subscribes via CommentEventJobProcessor.

resource commentEventTopic 'Microsoft.ServiceBus/namespaces/topics@2022-10-01-preview' = {
  parent: serviceBusNamespace
  name: commentEventTopicName
  properties: {
    defaultMessageTimeToLive: 'P14D'
    maxSizeInMegabytes: 1024
    requiresDuplicateDetection: false
  }
}

// Worker subscription — consumed by CommentEventJobProcessor in Testurio.Worker.
resource commentEventWorkerSubscription 'Microsoft.ServiceBus/namespaces/topics/subscriptions@2022-10-01-preview' = {
  parent: commentEventTopic
  name: commentEventSubscriptionName
  properties: {
    maxDeliveryCount: 5
    lockDuration: 'PT5M'
    defaultMessageTimeToLive: 'P14D'
    deadLetteringOnMessageExpiration: true
  }
}

// ─── Outputs ─────────────────────────────────────────────────────────────────

output serviceBusNamespaceId string = serviceBusNamespace.id
output testRunJobQueueName string = testRunJobQueue.name
output commentEventTopicName string = commentEventTopic.name
output commentEventWorkerSubscriptionName string = commentEventWorkerSubscription.name
