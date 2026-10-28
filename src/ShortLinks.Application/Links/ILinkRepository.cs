namespace ShortLinks.Application.Links;

public interface ILinkRepository
{
    Task<int> CountCreationsAsync(
        string creatorIpFingerprint,
        DateTime fromUtc,
        DateTime throughUtc,
        CancellationToken cancellationToken);

    Task<DateTime?> GetOldestCreationAsync(
        string creatorIpFingerprint,
        DateTime fromUtc,
        DateTime throughUtc,
        CancellationToken cancellationToken);

    Task<bool> CodeExistsAsync(
        string code,
        CancellationToken cancellationToken);

    Task<bool> TryCreateAsync(
        NewLinkRecord link,
        CancellationToken cancellationToken);

    Task<LinkRecord?> FindByCodeAsync(
        string code,
        CancellationToken cancellationToken);

    Task<bool> TryIncrementAccessCountAsync(
        string code,
        DateTime accessedAtUtc,
        CancellationToken cancellationToken);
}