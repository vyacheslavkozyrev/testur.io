namespace Testurio.Core.Interfaces;

public interface ILlmGenerationClient
{
    Task<string> CompleteAsync(string systemPrompt, string userMessage, CancellationToken cancellationToken = default);
}
