using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ShortLinks.IntegrationTests.Infrastructure;

namespace ShortLinks.IntegrationTests.Links;

public sealed class CreateLinkEndpointTests
{
    [Fact]
    public async Task Post_valid_link_returns_created_short_url_and_management_key()
    {
        using var factory = new ShortLinksApiFactory();
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync(
            "/api/links",
            new { destinationUrl = "https://example.org/docs?source=test#section" });

        var responseBody = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.Created,
            $"Expected 201 Created but received {(int)response.StatusCode}: {responseBody}");
        using var body = JsonDocument.Parse(responseBody);
        var result = body.RootElement;
        var code = result.GetProperty("code").GetString();

        Assert.NotNull(code);
        Assert.Equal(7, code.Length);
        Assert.Equal($"https://short.test/{code}", result.GetProperty("shortUrl").GetString());
        Assert.Equal(43, result.GetProperty("managementKey").GetString()?.Length);
    }

    [Theory]
    [InlineData("relative/path")]
    [InlineData("ftp://example.org/file")]
    public async Task Post_invalid_destination_returns_portuguese_problem_details(string destinationUrl)
    {
        using var factory = new ShortLinksApiFactory();
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync(
            "/api/links",
            new { destinationUrl });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Contains("URL", body.RootElement.GetProperty("detail").GetString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Post_61_simultaneous_creations_from_one_ip_allows_at_most_60()
    {
        using var factory = new ShortLinksApiFactory();
        using var client = factory.CreateClient();

        var requests = Enumerable.Range(0, 61).Select(async _ =>
        {
            using var response = await client.PostAsJsonAsync(
                "/api/links",
                new { destinationUrl = "https://example.org/" });

            return response.StatusCode;
        });

        var statuses = await Task.WhenAll(requests);

        Assert.Equal(60, statuses.Count(status => status == HttpStatusCode.Created));
        Assert.Single(statuses, status => (int)status == 429);
    }
}