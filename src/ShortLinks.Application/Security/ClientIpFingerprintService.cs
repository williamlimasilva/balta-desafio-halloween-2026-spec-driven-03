using System.Net;
using System.Security.Cryptography;

namespace ShortLinks.Application.Security;

public sealed class ClientIpFingerprintService : IDisposable
{
    private readonly byte[] _key;

    public ClientIpFingerprintService(byte[] key)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (key.Length < 32)
        {
            throw new ArgumentException("The HMAC key must contain at least 32 bytes.", nameof(key));
        }

        _key = key.ToArray();
    }

    public string Fingerprint(IPAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);

        var normalizedAddress = address.IsIPv4MappedToIPv6
            ? address.MapToIPv4()
            : address;
        var fingerprint = HMACSHA256.HashData(_key, normalizedAddress.GetAddressBytes());
        return Convert.ToHexString(fingerprint).ToLowerInvariant();
    }

    public void Dispose()
    {
        CryptographicOperations.ZeroMemory(_key);
    }
}