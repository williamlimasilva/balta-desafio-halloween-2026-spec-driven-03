using ShortLinks.Application.Links;
using ShortLinks.Application.Security;

namespace ShortLinks.UnitTests.Links;

public sealed class LinkCustomizationTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 10, 19, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("Abcd")]
    [InlineData("ABCDEFGHJKLMNPQRSTUV")]
    public async Task CreateAsync_accepts_custom_codes_at_length_boundaries(string code)
    {
        var repository = new FakeLinkRepository();
        var service = CreateService(repository);

        var result = await service.CreateAsync(CreateCommand(code));

        Assert.Equal(code, result.Code);
        Assert.Equal(code, repository.Snapshot.Single().Code);
    }

    [Theory]
    [InlineData("Abc")]
    [InlineData("ABCDEFGHJKLMNPQRSTUVW")]
    [InlineData("Abco")]
    public async Task CreateAsync_rejects_custom_codes_outside_length_or_alphabet(string code)
    {
        var repository = new FakeLinkRepository();
        var service = CreateService(repository);

        var exception = await Assert.ThrowsAsync<LinkServiceException>(
            () => service.CreateAsync(CreateCommand(code)));

        Assert.Equal(400, exception.StatusCode);
        Assert.Empty(repository.Snapshot);
    }

    [Fact]
    public async Task CreateAsync_rejects_duplicate_custom_code_without_replacing_existing_link()
    {
        var repository = new FakeLinkRepository();
        var service = CreateService(repository);
        await service.CreateAsync(CreateCommand("Guide42", "https://first.example/"));

        var exception = await Assert.ThrowsAsync<LinkServiceException>(
            () => service.CreateAsync(CreateCommand("Guide42", "https://second.example/")));

        Assert.Equal(409, exception.StatusCode);
        Assert.Single(repository.Snapshot);
        Assert.Equal("https://first.example/", repository.Snapshot.Single().DestinationUrl);
    }

    [Fact]
    public async Task CreateAsync_accepts_future_and_null_expiration()
    {
        var repository = new FakeLinkRepository();
        var service = CreateService(repository);
        var expiresAtUtc = Now.AddMinutes(5).UtcDateTime;

        var expiringLink = await service.CreateAsync(CreateCommand("Future42", "https://example.org/", expiresAtUtc));
        var nonExpiringLink = await service.CreateAsync(CreateCommand("Future2", "https://example.org/", null));

        Assert.Equal(expiresAtUtc, expiringLink.ExpiresAtUtc);
        Assert.Null(nonExpiringLink.ExpiresAtUtc);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    public async Task CreateAsync_rejects_past_or_current_expiration(int minutesFromNow)
    {
        var service = CreateService(new FakeLinkRepository());
        var expiresAtUtc = Now.AddMinutes(minutesFromNow).UtcDateTime;

        var exception = await Assert.ThrowsAsync<LinkServiceException>(
            () => service.CreateAsync(CreateCommand("Future42", "https://example.org/", expiresAtUtc)));

        Assert.Equal(400, exception.StatusCode);
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

    private static CreateLinkCommand CreateCommand(
        string? customCode,
        string destinationUrl = "https://example.org/",
        DateTime? expiresAtUtc = null)
    {
        return new CreateLinkCommand(destinationUrl, customCode, expiresAtUtc, "test-ip-fingerprint");
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