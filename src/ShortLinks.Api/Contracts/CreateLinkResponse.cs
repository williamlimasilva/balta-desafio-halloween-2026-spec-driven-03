namespace ShortLinks.Api.Contracts;

public sealed record CreateLinkResponse(
    string Code,
    string ShortUrl,
    string ManagementKey,
    DateTime CreatedAtUtc,
    DateTime? ExpiresAtUtc);