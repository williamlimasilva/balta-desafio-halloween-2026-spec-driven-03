using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ShortLinks.IntegrationTests.Infrastructure;

namespace ShortLinks.IntegrationTests.Links;

public sealed class LinkExpirationEndpointTests
{
    [Fact]
    public async Task Get_at_exact_expiry_returns_410_and_does_not_increment_count()
    {
        var nowUtc = new DateTimeOffset(2026, 10, 19, 12, 0, 0, TimeSpan.Zero);
        var clock = new AdjustableTimeProvider(nowUtc);
        using var factory = new ShortLinksApiFactory(timeProvider: clock);
        using var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        var expiresAtUtc = nowUtc.AddMinutes(1);

        using var createResponse = await client.PostAsJsonAsync(
            "/api/links",
            new { destinationUrl = "https://example.org/expiry", expiresAtUtc });
        createResponse.EnsureSuccessStatusCode();
        using var created = JsonDocument.Parse(await createResponse.Content.ReadAsStringAsync());
        var code = created.RootElement.GetProperty("code").GetString()!;
        var managementKey = created.RootElement.GetProperty("managementKey").GetString()!;

        using var activeRedirect = await client.GetAsync($"/{code}");
        Assert.Equal(HttpStatusCode.Found, activeRedirect.StatusCode);

        clock.SetUtcNow(expiresAtUtc);
        using var expiredRedirect = await client.GetAsync($"/{code}");
        Assert.Equal((HttpStatusCode)410, expiredRedirect.StatusCode);

        client.DefaultRequestHeaders.Add("X-Management-Key", managementKey);
        using var statsResponse = await client.GetAsync($"/api/links/{code}/stats");
        using var stats = JsonDocument.Parse(await statsResponse.Content.ReadAsStringAsync());
        Assert.Equal(1, stats.RootElement.GetProperty("accessCount").GetInt64());
    }
}