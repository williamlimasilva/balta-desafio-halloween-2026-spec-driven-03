using System.Security.Cryptography;
using System.Text;

namespace ShortLinks.Application.Security;

public sealed record GeneratedManagementKey(string PlainText, string Hash);

public sealed class ManagementKeyService
{
    private const int KeyLengthBytes = 32;

    public GeneratedManagementKey Generate()
    {
        var bytes = RandomNumberGenerator.GetBytes(KeyLengthBytes);
        var plainText = Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

        CryptographicOperations.ZeroMemory(bytes);

        return new GeneratedManagementKey(plainText, Hash(plainText));
    }

    public string Hash(string plainText)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(plainText));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public bool Verify(string plainText, string storedHash)
    {
        if (string.IsNullOrEmpty(plainText) || storedHash.Length != 64)
        {
            return false;
        }

        byte[] expectedHash;
        try
        {
            expectedHash = Convert.FromHexString(storedHash);
        }
        catch (FormatException)
        {
            return false;
        }

        var providedHash = SHA256.HashData(Encoding.UTF8.GetBytes(plainText));
        var matches = CryptographicOperations.FixedTimeEquals(providedHash, expectedHash);
        CryptographicOperations.ZeroMemory(providedHash);
        CryptographicOperations.ZeroMemory(expectedHash);
        return matches;
    }
}