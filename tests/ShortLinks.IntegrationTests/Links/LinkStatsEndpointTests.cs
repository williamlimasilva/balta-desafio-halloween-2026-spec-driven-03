using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ShortLinks.IntegrationTests.Infrastructure;

namespace ShortLinks.IntegrationTests.Links;

public sealed class LinkStatsEndpointTests
{
    [Fact]
    public async Task Get_stats_with_management_key_returns_current_count()
    {
        using var factory = new ShortLinksApiFactory();
        using var client = factory.CreateClient();
        var createdLink = await CreateLinkAsync(client);
        client.DefaultRequestHeaders.Add("X-Management-Key", createdLink.ManagementKey);

        using var response = await client.GetAsync($"/api/links/{createdLink.Code}/stats");
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(createdLink.Code, body.RootElement.GetProperty("code").GetString());
        Assert.Equal(0, body.RootElement.GetProperty("accessCount").GetInt64());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("incorrect-key")]
    public async Task Get_stats_without_valid_management_key_returns_unauthorized(string? managementKey)
    {
        using var factory = new ShortLinksApiFactory();
        using var client = factory.CreateClient();
        var createdLink = await CreateLinkAsync(client);
        if (managementKey is not null)
        {
            client.DefaultRequestHeaders.Add("X-Management-Key", managementKey);
        }

        using var response = await client.GetAsync($"/api/links/{createdLink.Code}/stats");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Get_stats_with_malformed_code_returns_bad_request()
    {
        using var factory = new ShortLinksApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/links/Bad_/stats");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static async Task<(string Code, string ManagementKey)> CreateLinkAsync(HttpClient client)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/links",
            new { destinationUrl = "https://example.org/stats" });
        response.EnsureSuccessStatusCode();

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return (
            body.RootElement.GetProperty("code").GetString()!,
            body.RootElement.GetProperty("managementKey").GetString()!);
    }
}