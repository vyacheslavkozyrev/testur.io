using Testurio.Core.Entities;

namespace Testurio.Core.Repositories;

/// <summary>
/// Repository contract for persisting <see cref="TestResult"/> documents to Cosmos DB.
/// The <c>TestResults</c> container is partitioned by <c>userId</c>.
/// Implemented by <c>TestResultRepository</c> in <c>Testurio.Infrastructure</c>.
/// </summary>
public interface ITestResultRepository
{
    /// <summary>
    /// Writes a new <see cref="TestResult"/> document to the <c>TestResults</c> Cosmos container.
    /// </summary>
    /// <param name="result">The test result to persist. <see cref="TestResult.Id"/> is used as the document id.</param>
    /// <param name="ct">Cancellation token.</param>
    Task SaveAsync(TestResult result, CancellationToken ct = default);
}
