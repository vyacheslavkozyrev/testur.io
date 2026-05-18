using Testurio.Core.Entities;
using Testurio.Core.Exceptions;
using Testurio.Core.Models;

namespace Testurio.Core.Interfaces;

/// <summary>
/// Contract for the ReportWriter pipeline stage (stage 6 / feature 0030).
/// Generates a structured verdict report via Claude, posts it as a comment to the
/// originating PM tool ticket, and persists a <see cref="TestResult"/> document to Cosmos DB.
/// </summary>
public interface IReportWriter
{
    /// <summary>
    /// Executes stage 6 of the pipeline: generates a report from <paramref name="execution"/>,
    /// formats and posts a PM tool comment, and writes a <see cref="TestResult"/> to Cosmos.
    /// </summary>
    /// <param name="story">
    /// The parsed story from stage 1. Provides <c>Title</c> used in the PM comment header
    /// and stored in the <see cref="TestResult"/> document.
    /// </param>
    /// <param name="execution">
    /// The merged execution result from stage 5. Contains all <c>ApiResults</c> and
    /// <c>UiE2eResults</c> that determine the verdict and per-scenario summaries.
    /// </param>
    /// <param name="projectConfig">
    /// Project configuration providing PM tool type, credentials, and issue identifiers
    /// for comment posting.
    /// </param>
    /// <param name="run">
    /// The active <see cref="TestRun"/> record. <c>PmCommentId</c> and <c>Status</c>
    /// are updated in-memory by this method before returning.
    /// </param>
    /// <param name="ct">Cancellation token forwarded to all I/O calls.</param>
    /// <returns>A <see cref="Task"/> that completes when the stage is finished.</returns>
    /// <exception cref="ReportWriterException">
    /// Thrown when Claude fails to produce a valid JSON report after the retry budget is
    /// exhausted, the verdict invariant check fails, or the Cosmos write fails.
    /// </exception>
    Task WriteAsync(
        ParsedStory story,
        ExecutionResult execution,
        Project projectConfig,
        TestRun run,
        CancellationToken ct = default);
}
