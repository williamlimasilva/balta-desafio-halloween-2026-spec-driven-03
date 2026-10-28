namespace ShortLinks.Infrastructure.Persistence;

public sealed class LinkEntity
{
    public long Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public string DestinationUrl { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? ExpiresAtUtc { get; set; }

    public long AccessCount { get; set; }

    public string ManagementKeyHash { get; set; } = string.Empty;

    public string CreatorIpFingerprint { get; set; } = string.Empty;
}