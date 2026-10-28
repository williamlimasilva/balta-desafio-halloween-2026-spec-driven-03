using System.Security.Cryptography;

namespace ShortLinks.Api.Options;

public sealed class LinkSecurityOptions
{
    public const string SectionName = "LinkSecurity";

    public string? ClientIpFingerprintKey { get; set; }

    public byte[] GetValidatedClientIpFingerprintKey()
    {
        if (string.IsNullOrWhiteSpace(ClientIpFingerprintKey))
        {
            throw new InvalidOperationException(
                "Configure LinkSecurity:ClientIpFingerprintKey with a base64-encoded key of at least 32 bytes.");
        }

        byte[] key;
        try
        {
            key = Convert.FromBase64String(ClientIpFingerprintKey);
        }
        catch (FormatException exception)
        {
            throw new InvalidOperationException(
                "LinkSecurity:ClientIpFingerprintKey must be valid base64.",
                exception);
        }

        if (key.Length < 32)
        {
            CryptographicOperations.ZeroMemory(key);
            throw new InvalidOperationException(
                "LinkSecurity:ClientIpFingerprintKey must contain at least 32 bytes.");
        }

        return key;
    }
}