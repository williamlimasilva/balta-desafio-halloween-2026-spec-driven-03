namespace ShortLinks.Application.Links;

public sealed class AsyncKeyedLock
{
    private readonly object _syncRoot = new();
    private readonly Dictionary<string, LockEntry> _entries = new(StringComparer.Ordinal);

    public async ValueTask<IDisposable> AcquireAsync(
        string key,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        LockEntry entry;
        lock (_syncRoot)
        {
            if (!_entries.TryGetValue(key, out entry!))
            {
                entry = new LockEntry();
                _entries.Add(key, entry);
            }

            entry.ReferenceCount++;
        }

        try
        {
            await entry.Semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            ReleaseReference(key, entry);
            throw;
        }

        return new Releaser(this, key, entry);
    }

    private void Release(string key, LockEntry entry)
    {
        entry.Semaphore.Release();
        ReleaseReference(key, entry);
    }

    private void ReleaseReference(string key, LockEntry entry)
    {
        lock (_syncRoot)
        {
            entry.ReferenceCount--;
            if (entry.ReferenceCount == 0
                && _entries.TryGetValue(key, out var currentEntry)
                && ReferenceEquals(currentEntry, entry))
            {
                _entries.Remove(key);
                entry.Semaphore.Dispose();
            }
        }
    }

    private sealed class LockEntry
    {
        public SemaphoreSlim Semaphore { get; } = new(1, 1);

        public int ReferenceCount { get; set; }
    }

    private sealed class Releaser : IDisposable
    {
        private readonly string _key;
        private readonly LockEntry _entry;
        private AsyncKeyedLock? _owner;

        public Releaser(AsyncKeyedLock owner, string key, LockEntry entry)
        {
            _owner = owner;
            _key = key;
            _entry = entry;
        }

        public void Dispose()
        {
            Interlocked.Exchange(ref _owner, null)?.Release(_key, _entry);
        }
    }
}