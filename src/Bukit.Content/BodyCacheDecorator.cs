using System.Collections.Concurrent;
using Bukit.Engine.Abstractions.Content;

namespace Bukit.Content;

public sealed record BodyCacheMetrics(long TotalRequests, long CacheHits, long CacheMisses, long UniqueBodies, long CacheSkips)
{
    public double Amplification => UniqueBodies == 0 ? 0 : (double)TotalRequests / UniqueBodies;
}

public sealed class BodyCacheDecorator : IContentBodyStore
{
    private readonly IContentBodyStore _inner;
    private readonly ConcurrentDictionary<string, Lazy<Task<ContentBody>>> _cache = new(StringComparer.Ordinal);
    private readonly int _maxEntries;
    private readonly ConcurrentQueue<string> _accessOrder = new();

    private long _totalRequests;
    private long _cacheHits;
    private long _cacheMisses;
    private long _cacheSkips;

    public BodyCacheDecorator(IContentBodyStore inner, int maxEntries = 10000)
    {
        _inner = inner;
        _maxEntries = maxEntries > 0 ? maxEntries : 10000;
    }

    public BodyCacheMetrics Metrics => new(
        Volatile.Read(ref _totalRequests),
        Volatile.Read(ref _cacheHits),
        Volatile.Read(ref _cacheMisses),
        _cache.Count,
        Volatile.Read(ref _cacheSkips));

    public async Task<ContentBody> GetAsync(ContentItem item, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _totalRequests);

        if (!string.IsNullOrWhiteSpace(item.ContentHtml))
        {
            Interlocked.Increment(ref _cacheHits);
            return new ContentBody(item.ContentHtml);
        }

        var key = item.BodyKey ?? item.Id;
        if (_cache.TryGetValue(key, out var lazy))
        {
            Interlocked.Increment(ref _cacheHits);
            return await lazy.Value;
        }

        var newLazy = new Lazy<Task<ContentBody>>(
            async () => new ContentBody((await _inner.GetAsync(item, cancellationToken)).Html),
            LazyThreadSafetyMode.ExecutionAndPublication);
        lazy = _cache.GetOrAdd(key, newLazy);
        if (ReferenceEquals(lazy, newLazy))
        {
            Interlocked.Increment(ref _cacheMisses);
            _accessOrder.Enqueue(key);
            TrimExcess();
        }
        else
        {
            Interlocked.Increment(ref _cacheHits);
        }

        return await lazy.Value;
    }

    private void TrimExcess()
    {
        if (_cache.Count <= _maxEntries)
        {
            return;
        }

        var removeCount = Math.Max(_maxEntries / 10, 1);
        for (var i = 0; i < removeCount; i++)
        {
            if (!_accessOrder.TryDequeue(out var key))
            {
                break;
            }

            _cache.TryRemove(key, out _);
            Interlocked.Increment(ref _cacheSkips);
        }
    }
}
