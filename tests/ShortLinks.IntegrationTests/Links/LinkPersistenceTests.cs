using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ShortLinks.Application.Links;
using ShortLinks.Application.Security;
using ShortLinks.Infrastructure.Persistence;
using ShortLinks.IntegrationTests.Infrastructure;

namespace ShortLinks.IntegrationTests.Links;

public sealed class LinkPersistenceTests
{
    [Fact]
    public async Task One_hundred_links_keep_destinations_expirations_and_counts_after_host_restart()
    {
        var databasePath = Path.Combine(
            Path.GetTempPath(),
            $"short-links-restart-{Guid.NewGuid():N}.db");
        var nowUtc = DateTime.UtcNow;
        var managementKeyHash = new ManagementKeyService().Hash("restart-test-key");

        using (var firstFactory = new ShortLinksApiFactory(databasePath, deleteDatabaseOnDispose: false))
        {
            using var client = firstFactory.CreateClient();
            using var scope = firstFactory.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<LinkDbContext>();
            var links = Enumerable.Range(0, 100)
                .Select(index => new LinkEntity
                {
                    Code = CreateCode(index),
                    DestinationUrl = $"https://destination-{index}.example/path?item={index}#section-{index}",
                    CreatedAtUtc = nowUtc.AddMinutes(-index),
                    ExpiresAtUtc = index % 2 == 0 ? nowUtc.AddHours(1) : nowUtc.AddMinutes(-1),
                    AccessCount = index,
                    ManagementKeyHash = managementKeyHash,
                    CreatorIpFingerprint = new string('a', 64)
                })
                .ToArray();

            dbContext.Links.AddRange(links);
            await dbContext.SaveChangesAsync();
        }

        using (var secondFactory = new ShortLinksApiFactory(databasePath))
        using (var client = secondFactory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        }))
        {
            using var scope = secondFactory.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<LinkDbContext>();
            var persistedLinks = await dbContext.Links.AsNoTracking().OrderBy(link => link.Code).ToListAsync();

            Assert.Equal(100, persistedLinks.Count);
            for (var index = 0; index < 100; index++)
            {
                var link = persistedLinks.Single(item => item.Code == CreateCode(index));
                Assert.Equal($"https://destination-{index}.example/path?item={index}#section-{index}", link.DestinationUrl);
                Assert.Equal(index, link.AccessCount);
                Assert.Equal(index % 2 == 0, link.ExpiresAtUtc > nowUtc);
            }

            var activeCode = CreateCode(0);
            using var activeResponse = await client.GetAsync($"/{activeCode}");
            Assert.Equal(HttpStatusCode.Found, activeResponse.StatusCode);
            Assert.Equal(
                "https://destination-0.example/path?item=0#section-0",
                activeResponse.Headers.Location?.OriginalString);

            var expiredCode = CreateCode(1);
            using var expiredResponse = await client.GetAsync($"/{expiredCode}");
            Assert.Equal((HttpStatusCode)410, expiredResponse.StatusCode);

            var activeCount = await dbContext.Links
                .Where(link => link.Code == activeCode)
                .Select(link => link.AccessCount)
                .SingleAsync();
            var expiredCount = await dbContext.Links
                .Where(link => link.Code == expiredCode)
                .Select(link => link.AccessCount)
                .SingleAsync();
            Assert.Equal(1, activeCount);
            Assert.Equal(1, expiredCount);
        }
    }

    private static string CreateCode(int value)
    {
        const string alphabet = ShortCodeGenerator.Alphabet;
        var characters = new char[7];
        characters[0] = 'T';

        for (var index = characters.Length - 1; index > 0; index--)
        {
            characters[index] = alphabet[value % alphabet.Length];
            value /= alphabet.Length;
        }

        return new string(characters);
    }
}