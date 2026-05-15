namespace Testurio.Core.Exceptions;

/// <summary>
/// Thrown by IStoryParser when a work item cannot be parsed and the pipeline run must be marked as failed.
/// This is a permanent, non-retriable failure — callers should dead-letter the queue message.
/// </summary>
public sealed class StoryParserException : Exception
{
    public StoryParserException(string message)
        : base(message)
    {
    }

    public StoryParserException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
