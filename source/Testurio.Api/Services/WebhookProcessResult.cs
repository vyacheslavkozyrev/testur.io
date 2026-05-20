namespace Testurio.Api.Services;

public enum WebhookProcessResult
{
    Ignored,
    Skipped,
    Enqueued,
    Queued,

    /// <summary>
    /// The webhook trigger was rejected because the project owner has reached or exceeded
    /// their plan's daily test-run quota. No <c>TestRun</c> document was created and no
    /// Service Bus message was sent. The webhook sender receives <c>200 OK</c> to prevent
    /// retry storms.
    /// </summary>
    QuotaExceeded
}
