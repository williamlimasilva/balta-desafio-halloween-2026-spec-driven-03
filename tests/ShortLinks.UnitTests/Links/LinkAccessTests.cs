using ShortLinks.Application.Links;
using ShortLinks.Application.Security;

namespace ShortLinks.UnitTests.Links;

public sealed class LinkAccessTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 10, 19, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task RedirectAsync_returns_destination_and_increments_access_once()
    {
        var repository = CreateRepository();
        var service = CreateService(repository);

        var result = await service.RedirectAsync("Abcdef");

        Assert.Equal("https://example.org/docs?q=1#section", result.DestinationUrl);
        Assert.Equal(1, repository.Snapshot.Single().AccessCount);
    }

    [Fact]
    public async Task GetStatsAsync_returns_count_for_matching_management_key()
    {
        var repository = CreateRepository();
        var service = CreateService(repository);

        var result = await service.GetStatsAsync("Abcdef", "known-management-key");

        Assert.Equal("Abcdef", result.Code);
        Assert.Equal(0, result.AccessCount);
    }

    [Theory]
    [InlineData("")]
    [InlineData("wrong-management-key")]
    public async Task GetStatsAsync_rejects_missing_or_invalid_management_key(string managementKey)
    {
        var service = CreateService(CreateRepository());

        var exception = await Assert.ThrowsAsync<LinkServiceException>(
            () => service.GetStatsAsync("Abcdef", managementKey));

        Assert.Equal(401, exception.StatusCode);
        Assert.DoesNotContain("3", exception.Detail);
    }

    [Fact]
    public async Task RedirectAsync_rejects_unknown_code()
    {
        var service = CreateService(CreateRepository());

        var exception = await Assert.ThrowsAsync<LinkServiceException>(
            () => service.RedirectAsync("Qwerty"));

        Assert.Equal(404, exception.StatusCode);
    }

    private static FakeLinkRepository CreateRepository()
    {
        var repository = new FakeLinkRepository();
        var keyHash = new ManagementKeyService().Hash("known-management-key");
        repository.Seed(new LinkRecord(
            1,
            "Abcdef",
            "https://example.org/docs?q=1#section",
            Now.UtcDateTime,
            null,
            0,
            keyHash,
            "test-ip-fingerprint"));
        return repository;
    }

    private static LinkService CreateService(FakeLinkRepository repository)
    {
        return new LinkService(
            repository,
            new ShortCodeGenerator(),
            new ManagementKeyService(),
            new AsyncKeyedLock(),
            new FrozenTimeProvider(Now),
            "https://short.test");
    }

    private sealed class FrozenTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public FrozenTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow() => _utcNow;
    }
}