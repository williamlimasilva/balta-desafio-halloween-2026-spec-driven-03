using ShortLinks.Application.Links;

namespace ShortLinks.UnitTests.Links;

public sealed class FakeLinkRepository : ILinkRepository
{
    private readonly object _syncRoot = new();
    private readonly List<LinkRecord> _links = new();
    private long _nextId = 1;

    public IReadOnlyList<LinkRecord> Snapshot
    {
        get
        {
            lock (_syncRoot)
            {
                return _links.ToArray();
            }
        }
    }

    public void Seed(LinkRecord link)
    {
        lock (_syncRoot)
        {
            _links.Add(link);
        }
    }

    public Task<int> CountCreationsAsync(
        string creatorIpFingerprint,
        DateTime fromUtc,
        DateTime throughUtc,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_syncRoot)
        {
            var count = _links.Count(link =>
                link.CreatorIpFingerprint == creatorIpFingerprint
                && link.CreatedAtUtc >= fromUtc
                && link.CreatedAtUtc <= throughUtc);

            return Task.FromResult(count);
        }
    }

    public Task<bool> CodeExistsAsync(
        string code,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_syncRoot)
        {
            return Task.FromResult(_links.Any(link => link.Code == code));
        }
    }

    public Task<DateTime?> GetOldestCreationAsync(
        string creatorIpFingerprint,
        DateTime fromUtc,
        DateTime throughUtc,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_syncRoot)
        {
            var oldest = _links
                .Where(link => link.CreatorIpFingerprint == creatorIpFingerprint
                    && link.CreatedAtUtc >= fromUtc
                    && link.CreatedAtUtc <= throughUtc)
                .Select(link => (DateTime?)link.CreatedAtUtc)
                .Min();

            return Task.FromResult(oldest);
        }
    }

    public Task<bool> TryCreateAsync(
        NewLinkRecord link,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_syncRoot)
        {
            if (_links.Any(existing => existing.Code == link.Code))
            {
                return Task.FromResult(false);
            }

            _links.Add(new LinkRecord(
                _nextId++,
                link.Code,
                link.DestinationUrl,
                link.CreatedAtUtc,
                link.ExpiresAtUtc,
                0,
                link.ManagementKeyHash,
                link.CreatorIpFingerprint));

            return Task.FromResult(true);
        }
    }

    public Task<LinkRecord?> FindByCodeAsync(
        string code,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_syncRoot)
        {
            return Task.FromResult(_links.SingleOrDefault(link => link.Code == code));
        }
    }

    public Task<bool> TryIncrementAccessCountAsync(
        string code,
        DateTime accessedAtUtc,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_syncRoot)
        {
            var index = _links.FindIndex(link =>
                link.Code == code
                && (link.ExpiresAtUtc == null || link.ExpiresAtUtc > accessedAtUtc));

            if (index < 0)
            {
                return Task.FromResult(false);
            }

            _links[index] = _links[index] with
            {
                AccessCount = _links[index].AccessCount + 1
            };

            return Task.FromResult(true);
        }
    }
}