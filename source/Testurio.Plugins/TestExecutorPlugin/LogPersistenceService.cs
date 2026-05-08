using Microsoft.Extensions.Logging;
using Testurio.Core.Entities;
using Testurio.Core.Repositories;
using Testurio.Infrastructure.Blob;

namespace Testurio.Plugins.TestExecutorPlugin;

/// <summary>
/// Owns the inline-vs-blob routing decision for response bodies and persists
/// <see cref="ExecutionLogEntry"/> records to the repository.
/// All failures are absorbed non-fatally: a system warning is logged but the
/// calling step result is never affected (AC-004).
/// </summary>
public partial class LogPersistenceService
{
    private readonly IExecutionLogRepository _repository;
    private readonly BlobStorageClient _blobClient;
    private readonly ILogger<LogPersistenceService> _logger;

    public LogPersistenceService(
        IExecutionLogRepository repository,
        BlobStorageClient blobClient,
        ILogger<LogPersistenceService> logger)
    {
        _repository = repository;
        _blobClient = blobClient;
        _logger = logger;
    }

    /// <summary>
    /// Persists <paramref name="entry"/> applying inline/blob routing for the response body.
    /// Never throws — any persistence failure is recorded as a system warning.
    /// </summary>
    public async Task PersistAsync(ExecutionLogEntry entry, CancellationToken cancellationToken = default)
    {
        try
        {
            await RouteResponseBodyAsync(entry, cancellationToken);
            await _repository.PersistAsync(entry, cancellationToken);
        }
        catch (Exception ex)
        {
            // AC-004: persistence failure must not affect the step result.
            LogPersistenceFailed(_logger, entry.TestRunId, entry.ScenarioId, entry.StepIndex, ex);
        }
    }

    /// <summary>
    /// Decides whether the response body should be stored inline or uploaded to blob storage.
    /// Mutates <paramref name="entry"/> directly — the entry is not yet written to the repository.
    /// </summary>
    private async Task RouteResponseBodyAsync(ExecutionLogEntry entry, CancellationToken cancellationToken)
    {
        // ResponseBodyInline was pre-set by the caller; check its byte length.
        var inlineBody = entry.ResponseBodyInline;

        if (inlineBody is null)
            return; // No body — nothing to route.

        var byteLength = System.Text.Encoding.UTF8.GetByteCount(inlineBody);

        if (byteLength <= BlobStorageClient.InlineThresholdBytes)
        {
            // AC-005: body fits within the inline threshold — keep as-is.
            return;
        }

        // AC-006: body exceeds threshold — attempt blob upload.
        var blobName = $"logs/{entry.TestRunId}/{entry.ScenarioId}/{entry.StepIndex}.txt";
        var blobUrl = await _blobClient.UploadAsync(blobName, inlineBody, cancellationToken);

        if (blobUrl is not null)
        {
            // AC-006: upload succeeded — store reference URL and clear inline body.
            entry.ResponseBodyInline = null;
            entry.ResponseBodyBlobUrl = blobUrl;
        }
        else
        {
            // AC-008: upload failed — truncate to threshold and flag entry.
            var truncated = TruncateToThreshold(inlineBody);
            entry.ResponseBodyInline = truncated;
            entry.ResponseTruncated = true;
            LogBodyTruncated(_logger, entry.TestRunId, entry.ScenarioId, entry.StepIndex, byteLength);
        }
    }

    private static string TruncateToThreshold(string content)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(content);
        if (bytes.Length <= BlobStorageClient.InlineThresholdBytes)
            return content;

        // Walk back from the threshold boundary to the start of a complete UTF-8 codepoint.
        // Continuation bytes have the pattern 10xxxxxx (0x80–0xBF); a leading byte is 0xxxxxxx
        // or 11xxxxxx.  Slicing blindly at InlineThresholdBytes can land mid-sequence and
        // produce a malformed string.
        var limit = BlobStorageClient.InlineThresholdBytes;
        while (limit > 0 && (bytes[limit] & 0xC0) == 0x80)
            limit--;

        return System.Text.Encoding.UTF8.GetString(bytes, 0, limit);
    }

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Execution log persistence failed for run {TestRunId} scenario {ScenarioId} step {StepIndex}")]
    private static partial void LogPersistenceFailed(
        ILogger logger, string testRunId, string scenarioId, int stepIndex, Exception ex);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Response body for run {TestRunId} scenario {ScenarioId} step {StepIndex} truncated from {OriginalBytes} bytes after blob upload failure")]
    private static partial void LogBodyTruncated(
        ILogger logger, string testRunId, string scenarioId, int stepIndex, int originalBytes);
}
