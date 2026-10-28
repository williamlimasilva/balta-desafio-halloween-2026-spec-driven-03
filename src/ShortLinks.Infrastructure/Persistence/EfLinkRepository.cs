using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ShortLinks.Application.Links;

namespace ShortLinks.Infrastructure.Persistence;

public sealed class EfLinkRepository : ILinkRepository
{
    public EfLinkRepository(LinkDbContext dbContext)
    {
        DbContext = dbContext;
    }

    private LinkDbContext DbContext { get; }

    public Task<int> CountCreationsAsync(
        string creatorIpFingerprint,
        DateTime fromUtc,
        DateTime throughUtc,
        CancellationToken cancellationToken)
    {
        return DbContext.Links.CountAsync(
            link => link.CreatorIpFingerprint == creatorIpFingerprint
                && link.CreatedAtUtc >= fromUtc
                && link.CreatedAtUtc <= throughUtc,
            cancellationToken);
    }

    public Task<DateTime?> GetOldestCreationAsync(
        string creatorIpFingerprint,
        DateTime fromUtc,
        DateTime throughUtc,
        CancellationToken cancellationToken)
    {
        return DbContext.Links
            .Where(link => link.CreatorIpFingerprint == creatorIpFingerprint
                && link.CreatedAtUtc >= fromUtc
                && link.CreatedAtUtc <= throughUtc)
            .OrderBy(link => link.CreatedAtUtc)
            .Select(link => (DateTime?)link.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<bool> CodeExistsAsync(
        string code,
        CancellationToken cancellationToken)
    {
        return DbContext.Links.AnyAsync(link => link.Code == code, cancellationToken);
    }

    public async Task<bool> TryCreateAsync(
        NewLinkRecord link,
        CancellationToken cancellationToken)
    {
        var entity = new LinkEntity
        {
            Code = link.Code,
            DestinationUrl = link.DestinationUrl,
            CreatedAtUtc = DateTime.SpecifyKind(link.CreatedAtUtc, DateTimeKind.Utc),
            ExpiresAtUtc = link.ExpiresAtUtc is null
                ? null
                : DateTime.SpecifyKind(link.ExpiresAtUtc.Value, DateTimeKind.Utc),
            AccessCount = 0,
            ManagementKeyHash = link.ManagementKeyHash,
            CreatorIpFingerprint = link.CreatorIpFingerprint
        };

        DbContext.Links.Add(entity);

        try
        {
            await DbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            DbContext.Entry(entity).State = EntityState.Detached;
            return false;
        }
    }

    public Task<LinkRecord?> FindByCodeAsync(
        string code,
        CancellationToken cancellationToken)
    {
        return DbContext.Links
            .AsNoTracking()
            .Where(link => link.Code == code)
            .Select(link => new LinkRecord(
                link.Id,
                link.Code,
                link.DestinationUrl,
                link.CreatedAtUtc,
                link.ExpiresAtUtc,
                link.AccessCount,
                link.ManagementKeyHash,
                link.CreatorIpFingerprint))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<bool> TryIncrementAccessCountAsync(
        string code,
        DateTime accessedAtUtc,
        CancellationToken cancellationToken)
    {
        return IncrementAccessCountAsync(code, accessedAtUtc, cancellationToken);
    }

    private async Task<bool> IncrementAccessCountAsync(
        string code,
        DateTime accessedAtUtc,
        CancellationToken cancellationToken)
    {
        var utcAccessTime = DateTime.SpecifyKind(accessedAtUtc, DateTimeKind.Utc);
        var updated = await DbContext.Links
            .Where(link => link.Code == code
                && (link.ExpiresAtUtc == null || link.ExpiresAtUtc > utcAccessTime))
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(link => link.AccessCount, link => link.AccessCount + 1),
                cancellationToken)
            .ConfigureAwait(false);

        return updated == 1;
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception)
    {
        return exception.InnerException is SqliteException sqliteException
            && sqliteException.SqliteErrorCode == 19
            && (sqliteException.SqliteExtendedErrorCode == 1555
                || sqliteException.SqliteExtendedErrorCode == 2067);
    }
}