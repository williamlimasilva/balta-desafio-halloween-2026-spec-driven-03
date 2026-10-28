namespace ShortLinks.Application.Links;

public sealed class LinkServiceException : Exception
{
    public LinkServiceException(
        int statusCode,
        string title,
        string detail,
        int? retryAfterSeconds = null)
        : base(detail)
    {
        StatusCode = statusCode;
        Title = title;
        Detail = detail;
        RetryAfterSeconds = retryAfterSeconds;
    }

    public int StatusCode { get; }

    public string Title { get; }

    public string Detail { get; }

    public int? RetryAfterSeconds { get; }
}