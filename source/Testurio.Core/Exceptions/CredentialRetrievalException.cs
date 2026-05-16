namespace Testurio.Core.Exceptions;

/// <summary>
/// Thrown when the <see cref="Interfaces.IProjectAccessCredentialProvider"/> cannot retrieve
/// credentials from Key Vault — e.g. vault unreachable, secret URI invalid, or secret deleted.
/// Pipeline stages catch this and mark the test run as Failed.
/// </summary>
public sealed class CredentialRetrievalException : Exception
{
    public CredentialRetrievalException(string message) : base(message) { }

    public CredentialRetrievalException(string message, Exception innerException)
        : base(message, innerException) { }
}
