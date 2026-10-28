namespace ShortLinks.IntegrationTests.Infrastructure;

public sealed class AdjustableTimeProvider : TimeProvider
{
    private readonly object _syncRoot = new();
    private DateTimeOffset _utcNow;

    public AdjustableTimeProvider(DateTimeOffset utcNow)
    {
        _utcNow = utcNow.ToUniversalTime();
    }

    public override DateTimeOffset GetUtcNow()
    {
        lock (_syncRoot)
        {
            return _utcNow;
        }
    }

    public void SetUtcNow(DateTimeOffset utcNow)
    {
        lock (_syncRoot)
        {
            _utcNow = utcNow.ToUniversalTime();
        }
    }
}