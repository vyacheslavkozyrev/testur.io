using System.Security.Cryptography;
using System.Text;

namespace Testurio.Infrastructure.Security;

/// <summary>
/// Generates and validates per-project webhook secrets.
/// Secrets are stored in Key Vault — this class only handles generation and HMAC validation.
/// </summary>
public sealed class WebhookSecretGenerator
{
    private const int SecretByteLength = 32; // 256-bit secret → 64-char hex string

    /// <summary>
    /// Generates a cryptographically random webhook secret as a lowercase hex string.
    /// </summary>
    public string GenerateSecret()
    {
        var bytes = RandomNumberGenerator.GetBytes(SecretByteLength);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    /// <summary>
    /// Validates an incoming webhook payload signature against the stored secret.
    /// Uses HMAC-SHA256 in constant-time comparison to prevent timing attacks.
    /// </summary>
    /// <param name="payload">Raw request body bytes.</param>
    /// <param name="signature">Signature value from the webhook request header (hex-encoded).</param>
    /// <param name="secret">The stored webhook secret (plaintext — resolved from Key Vault by the caller).</param>
    /// <returns>True if the signature is valid; false otherwise.</returns>
    public bool ValidateSignature(byte[] payload, string signature, string secret)
    {
        if (string.IsNullOrWhiteSpace(signature) || string.IsNullOrWhiteSpace(secret))
            return false;

        var keyBytes = Encoding.UTF8.GetBytes(secret);
        using var hmac = new HMACSHA256(keyBytes);
        var expected = Convert.ToHexString(hmac.ComputeHash(payload)).ToLowerInvariant();

        // Strip a leading "sha256=" prefix if present (GitHub/ADO convention)
        var normalised = signature.StartsWith("sha256=", StringComparison.OrdinalIgnoreCase)
            ? signature[7..]
            : signature;

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected),
            Encoding.UTF8.GetBytes(normalised));
    }
}
