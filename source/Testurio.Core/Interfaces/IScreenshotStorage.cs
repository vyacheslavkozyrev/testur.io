namespace Testurio.Core.Interfaces;

/// <summary>
/// Contract for uploading Playwright screenshot PNG bytes to Azure Blob Storage.
/// Used by <see cref="IPlaywrightExecutor"/> on assertion-step failures (feature 0029).
/// </summary>
public interface IScreenshotStorage
{
    /// <summary>
    /// Uploads the PNG screenshot bytes to the <c>test-screenshots</c> Blob Storage container
    /// at path <c>{userId}/{runId}/{scenarioId}/step-{stepIndex}.png</c>.
    /// </summary>
    /// <param name="userId">Owning user identifier — first path segment.</param>
    /// <param name="runId">Current test run identifier — second path segment.</param>
    /// <param name="scenarioId">Scenario identifier — third path segment.</param>
    /// <param name="stepIndex">Zero-based step index — used as the blob file name.</param>
    /// <param name="png">Raw PNG image bytes captured by Playwright.</param>
    /// <param name="ct">Cancellation token forwarded to the Azure Blob SDK.</param>
    /// <returns>
    /// The full Blob Storage URI of the uploaded screenshot
    /// (e.g. <c>https://account.blob.core.windows.net/test-screenshots/userId/runId/scenarioId/step-0.png</c>).
    /// </returns>
    Task<string> UploadAsync(
        string userId,
        Guid runId,
        string scenarioId,
        int stepIndex,
        byte[] png,
        CancellationToken ct = default);
}
