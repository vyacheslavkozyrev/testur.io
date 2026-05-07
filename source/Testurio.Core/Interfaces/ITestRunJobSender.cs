using Testurio.Core.Models;

namespace Testurio.Core.Interfaces;

public interface ITestRunJobSender
{
    Task SendAsync(TestRunJobMessage message, CancellationToken cancellationToken = default);
}
