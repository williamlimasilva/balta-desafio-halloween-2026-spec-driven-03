using ShortLinks.Application.Links;
using ShortLinks.Application.Security;

namespace ShortLinks.UnitTests.Links;

public sealed class LinkCreationTests
{
    [Fact]
    public void Management_key_service_verifies_only_the_generated_secret()
    {
        var service = new ManagementKeyService();
        var generatedKey = service.Generate();

        Assert.True(service.Verify(generatedKey.PlainText, generatedKey.Hash));
        Assert.False(service.Verify("wrong-key", generatedKey.Hash));
        Assert.Equal(64, generatedKey.Hash.Length);
        Assert.Equal(43, generatedKey.PlainText.Length);
    }

    [Fact]
    public void Client_ip_fingerprint_is_stable_normalizes_mapped_ipv4_and_does_not_reveal_address()
    {
        var key = Enumerable.Range(0, 32).Select(value => (byte)value).ToArray();
        using var fingerprintService = new ClientIpFingerprintService(key);
        var address = System.Net.IPAddress.Parse("192.0.2.42");

        var fingerprint = fingerprintService.Fingerprint(address);

        Assert.Equal(64, fingerprint.Length);
        Assert.Matches("^[0-9a-f]{64}$", fingerprint);
        Assert.Equal(fingerprint, fingerprintService.Fingerprint(address.MapToIPv6()));
        Assert.DoesNotContain(address.ToString(), fingerprint, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateAsync_rejects_empty_destination()
    {
        var repository = new FakeLinkRepository();
        var service = CreateService(repository);

        var exception = await Assert.ThrowsAsync<LinkServiceException>(
            () => service.CreateAsync(CreateCommand(string.Empty)));

        Assert.Equal(400, exception.StatusCode);
        Assert.Empty(repository.Snapshot);
    }

    private static readonly DateTimeOffset Now =
        new(2026, 10, 19, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task CreateAsync_generates_seven_character_code_and_returns_management_key_once()
    {
        var repository = new FakeLinkRepository();
        var service = CreateService(repository);

        var result = await service.CreateAsync(CreateCommand("https://example.org/path?q=1#part"));

        Assert.Equal(7, result.Code.Length);
        Assert.All(result.Code, character => Assert.Contains(character, ShortCodeGenerator.Alphabet));
        Assert.Equal($"https://short.test/{result.Code}", result.ShortUrl);
        Assert.Equal(43, result.ManagementKey.Length);
        Assert.Single(repository.Snapshot);
        Assert.Equal(64, repository.Snapshot[0].ManagementKeyHash.Length);
        Assert.DoesNotContain(result.ManagementKey, repository.Snapshot[0].ManagementKeyHash);
    }

    [Theory]
    [InlineData("relative/path")]
    [InlineData("ftp://example.org/file")]
    [InlineData("https:///missing-host")]
    public async Task CreateAsync_rejects_non_absolute_or_non_http_destination(string destinationUrl)
    {
        var repository = new FakeLinkRepository();
        var service = CreateService(repository);

        var exception = await Assert.ThrowsAsync<LinkServiceException>(
            () => service.CreateAsync(CreateCommand(destinationUrl)));

        Assert.Equal(400, exception.StatusCode);
        Assert.Contains("URL", exception.Detail, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(repository.Snapshot);
    }

    [Fact]
    public async Task CreateAsync_accepts_destination_of_exactly_2048_characters()
    {
        var repository = new FakeLinkRepository();
        var service = CreateService(repository);
        var prefix = "https://example.org/";
        var destinationUrl = prefix + new string('a', 2048 - prefix.Length);

        var result = await service.CreateAsync(CreateCommand(destinationUrl));

        Assert.Equal(destinationUrl, repository.Snapshot.Single().DestinationUrl);
        Assert.Equal(7, result.Code.Length);
    }

    [Fact]
    public async Task CreateAsync_rejects_url_longer_than_2048_characters()
    {
        var repository = new FakeLinkRepository();
        var service = CreateService(repository);
        var destinationUrl = "https://example.org/" + new string('a', 2048);

        var exception = await Assert.ThrowsAsync<LinkServiceException>(
            () => service.CreateAsync(CreateCommand(destinationUrl)));

        Assert.Equal(400, exception.StatusCode);
        Assert.Empty(repository.Snapshot);
    }

    [Fact]
    public async Task CreateAsync_allows_at_most_60_concurrent_creations_per_ip_in_a_rolling_hour()
    {
        var repository = new FakeLinkRepository();
        var service = CreateService(repository);

        var outcomes = await Task.WhenAll(Enumerable.Range(0, 61).Select(async _ =>
        {
            try
            {
                await service.CreateAsync(CreateCommand("https://example.org/", "same-ip"));
                return true;
            }
            catch (LinkServiceException exception) when (exception.StatusCode == 429)
            {
                return false;
            }
        }));

        Assert.Equal(60, outcomes.Count(accepted => accepted));
        Assert.Equal(1, outcomes.Count(accepted => !accepted));
        Assert.Equal(60, repository.Snapshot.Count);
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
        string destinationUrl,
        string creatorIpFingerprint = "test-ip-fingerprint")
    {
        return new CreateLinkCommand(destinationUrl, null, null, creatorIpFingerprint);
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