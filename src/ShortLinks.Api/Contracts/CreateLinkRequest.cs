namespace ShortLinks.Api.Contracts;

public sealed class CreateLinkRequest
{
    public string? DestinationUrl { get; init; }

    public string? CustomCode { get; init; }

    public DateTimeOffset? ExpiresAtUtc { get; init; }
}