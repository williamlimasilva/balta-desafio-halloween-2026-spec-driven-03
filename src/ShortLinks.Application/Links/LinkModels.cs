namespace ShortLinks.Application.Links;

public sealed record CreateLinkCommand(
    string DestinationUrl,
    string? CustomCode,
    DateTime? ExpiresAtUtc,
    string CreatorIpFingerprint);

public sealed record CreateLinkResult(
    string Code,
    string ShortUrl,
    string ManagementKey,
    DateTime CreatedAtUtc,
    DateTime? ExpiresAtUtc);

public sealed record NewLinkRecord(
    string Code,
    string DestinationUrl,
    DateTime CreatedAtUtc,
    DateTime? ExpiresAtUtc,
    string ManagementKeyHash,
    string CreatorIpFingerprint);

public sealed record LinkRecord(
    long Id,
    string Code,
    string DestinationUrl,
    DateTime CreatedAtUtc,
    DateTime? ExpiresAtUtc,
    long AccessCount,
    string ManagementKeyHash,
    string CreatorIpFingerprint);

public sealed record LinkStatsResult(string Code, long AccessCount);

public sealed record RedirectResult(string DestinationUrl);