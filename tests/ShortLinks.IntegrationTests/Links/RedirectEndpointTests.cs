using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ShortLinks.IntegrationTests.Infrastructure;

namespace ShortLinks.IntegrationTests.Links;

public sealed class RedirectEndpointTests
{
    [Fact]
    public async Task Get_active_link_returns_302_and_preserves_full_destination()
    {
        using var factory = new ShortLinksApiFactory();
        using var client = CreateClient(factory);
        const string destination = "https://example.org/docs/path?q=one#section";
        var createdLink = await CreateLinkAsync(client, destination);

        using var response = await client.GetAsync($"/{createdLink.Code}");

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Equal(destination, response.Headers.Location?.OriginalString);

        client.DefaultRequestHeaders.Add("X-Management-Key", createdLink.ManagementKey);
        using var statsResponse = await client.GetAsync($"/api/links/{createdLink.Code}/stats");
        using var stats = JsonDocument.Parse(await statsResponse.Content.ReadAsStringAsync());
        Assert.Equal(1, stats.RootElement.GetProperty("accessCount").GetInt64());
    }

    [Fact]
    public async Task Get_one_hundred_concurrent_active_redirects_increments_count_by_one_hundred()
    {
        using var factory = new ShortLinksApiFactory();
        using var client = CreateClient(factory);
        var createdLink = await CreateLinkAsync(client, "https://example.org/concurrent");

        var requests = Enumerable.Range(0, 100)
            .Select(_ => client.GetAsync($"/{createdLink.Code}"));
        var responses = await Task.WhenAll(requests);

        try
        {
            Assert.All(responses, response => Assert.Equal(HttpStatusCode.Found, response.StatusCode));
        }
        finally
        {
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }

        client.DefaultRequestHeaders.Add("X-Management-Key", createdLink.ManagementKey);
        using var statsResponse = await client.GetAsync($"/api/links/{createdLink.Code}/stats");
        using var stats = JsonDocument.Parse(await statsResponse.Content.ReadAsStringAsync());
        Assert.Equal(100, stats.RootElement.GetProperty("accessCount").GetInt64());
    }

    [Fact]
    public async Task Get_malformed_code_returns_bad_request_and_unknown_valid_code_returns_not_found()
    {
        using var factory = new ShortLinksApiFactory();
        using var client = CreateClient(factory);

        using var malformedResponse = await client.GetAsync("/Bad_");
        using var unknownResponse = await client.GetAsync("/Qwerty");

        Assert.Equal(HttpStatusCode.BadRequest, malformedResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknownResponse.StatusCode);
    }

    private static HttpClient CreateClient(ShortLinksApiFactory factory)
    {
        return factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    private static async Task<(string Code, string ManagementKey)> CreateLinkAsync(
        HttpClient client,
        string destinationUrl)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/links",
            new { destinationUrl });
        response.EnsureSuccessStatusCode();

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return (
            body.RootElement.GetProperty("code").GetString()!,
            body.RootElement.GetProperty("managementKey").GetString()!);
    }
}