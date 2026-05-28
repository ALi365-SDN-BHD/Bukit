using System.Collections.Concurrent;
using Bukit.Engine.Abstractions.Content;

namespace Bukit.Content;

public sealed record BodyCacheMetrics(long TotalRequests, long CacheHits, long CacheMisses, long UniqueBodies)
{
    public double Amplification => UniqueBodies == 0 ? 0 : (double)TotalRequests / UniqueBodies;
}

public sealed class BodyCacheDecorator : IContentBodyStore
{
    private readonly IContentBodyStore _inner;
    private readonly ConcurrentDictionary<string, Lazy<Task<ContentBody>>> _cache = new(StringComparer.Ordinal);

    private long _totalRequests;
    private long _cacheHits;
    private long _cacheMisses;

    public BodyCacheDecorator(IContentBodyStore inner)
    {
        _inner = inner;
    }

    public BodyCacheMetrics Metrics => new(
        Volatile.Read(ref _totalRequests),
        Volatile.Read(ref _cacheHits),
        Volatile.Read(ref _cacheMisses),
        _cache.Count);

    public async Task<ContentBody> GetAsync(ContentItem item, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _totalRequests);

        if (!string.IsNullOrWhiteSpace(item.ContentHtml))
        {
            Interlocked.Increment(ref _cacheHits);
            return new ContentBody(item.ContentHtml);
        }

        var key = item.BodyKey ?? item.Id;
        var lazy = _cache.GetOrAdd(key, _ =>
        {
            Interlocked.Increment(ref _cacheMisses);
            return new Lazy<Task<ContentBody>>(
                async () => new ContentBody((await _inner.GetAsync(item, cancellationToken)).Html),
                LazyThreadSafetyMode.ExecutionAndPublication);
        });
        Interlocked.Increment(ref _cacheHits);
        return await lazy.Value;
    }
}
