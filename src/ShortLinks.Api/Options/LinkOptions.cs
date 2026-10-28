namespace ShortLinks.Api.Options;

public sealed class LinkOptions
{
    public const string SectionName = "Links";

    public string DatabasePath { get; set; } = "Data/shortlinks.db";

    public string PublicBaseUrl { get; set; } = "http://localhost:5000";
}