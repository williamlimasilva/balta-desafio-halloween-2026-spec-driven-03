using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using ShortLinks.IntegrationTests.Infrastructure;

namespace ShortLinks.IntegrationTests.Links;

public sealed class LinkCustomizationEndpointTests
{
    [Fact]
    public async Task Post_custom_code_uses_exact_code_and_duplicate_does_not_replace_link()
    {
        using var factory = new ShortLinksApiFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        using var firstResponse = await client.PostAsJsonAsync(
            "/api/links",
            new { destinationUrl = "https://first.example/", customCode = "Guide42" });
        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);

        using var duplicateResponse = await client.PostAsJsonAsync(
            "/api/links",
            new { destinationUrl = "https://second.example/", customCode = "Guide42" });
        Assert.Equal(HttpStatusCode.Conflict, duplicateResponse.StatusCode);

        using var redirect = await client.GetAsync("/Guide42");
        Assert.Equal(HttpStatusCode.Found, redirect.StatusCode);
        Assert.Equal("https://first.example/", redirect.Headers.Location?.OriginalString);
    }

    [Theory]
    [InlineData("Abc")]
    [InlineData("Abco")]
    [InlineData("ABCDEFGHJKLMNPQRSTUVW")]
    public async Task Post_invalid_custom_code_returns_bad_request(string customCode)
    {
        using var factory = new ShortLinksApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/links",
            new { destinationUrl = "https://example.org/", customCode });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Post_past_expiration_returns_bad_request()
    {
        using var factory = new ShortLinksApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/links",
            new
            {
                destinationUrl = "https://example.org/",
                expiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1)
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}