using System.Collections.Concurrent;
using System.Collections.Generic;
using Bukit.Engine.Abstractions.Content;

namespace Bukit.Content;

public sealed record BodyCacheMetrics(long TotalRequests, long CacheHits, long CacheMisses, long InlineBypasses, long UniqueBodies, long CacheSkips)
{
    public double Amplification => UniqueBodies == 0 ? 0 : (double)TotalRequests / UniqueBodies;
}

public sealed class BodyCacheDecorator : IContentBodyStore, IAsyncDisposable
{
    private readonly IContentBodyStore _inner;
    private readonly ConcurrentDictionary<string, Lazy<Task<ContentBody>>> _cache = new(StringComparer.Ordinal);
    private readonly int _maxEntries;
    private readonly object _lruLock = new();
    private readonly LinkedList<string> _lruList = new();
    private readonly ConcurrentDictionary<string, LinkedListNode<string>> _lruNodes = new(StringComparer.Ordinal);

    private long _totalRequests;
    private long _cacheHits;
    private long _cacheMisses;
    private long _inlineBypasses;
    private long _cacheSkips;
    private int _disposeState;

    public BodyCacheDecorator(IContentBodyStore inner, int maxEntries = 10000)
    {
        _inner = inner;
        _maxEntries = maxEntries > 0 ? maxEntries : 10000;
    }

    public BodyCacheMetrics Metrics => new(
        Volatile.Read(ref _totalRequests),
        Volatile.Read(ref _cacheHits),
        Volatile.Read(ref _cacheMisses),
        Volatile.Read(ref _inlineBypasses),
        _cache.Count,
        Volatile.Read(ref _cacheSkips));

    public async Task<ContentBody> GetAsync(ContentDocument document, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _totalRequests);

        if (!string.IsNullOrWhiteSpace(document.Body.Html))
        {
            Interlocked.Increment(ref _inlineBypasses);
            return new ContentBody(document.Body.Html);
        }

        var key = document.Body.BodyKey ?? document.Id;
        if (_cache.TryGetValue(key, out var lazy))
        {
            Interlocked.Increment(ref _cacheHits);
            lock (_lruLock)
            {
                if (_lruNodes.TryGetValue(key, out var node))
                {
                    _lruList.Remove(node);
                    _lruList.AddLast(node);
                }
            }
            return await lazy.Value;
        }

        var newLazy = new Lazy<Task<ContentBody>>(
            async () => new ContentBody((await _inner.GetAsync(document, cancellationToken)).Html),
            LazyThreadSafetyMode.ExecutionAndPublication);
        lazy = _cache.GetOrAdd(key, newLazy);
        if (ReferenceEquals(lazy, newLazy))
        {
            Interlocked.Increment(ref _cacheMisses);
            lock (_lruLock)
            {
                var node = _lruList.AddLast(key);
                _lruNodes[key] = node;
            }
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
            string? keyToRemove;
            lock (_lruLock)
            {
                if (_lruList.First is null)
                {
                    break;
                }
                keyToRemove = _lruList.First.Value;
                _lruList.RemoveFirst();
                _lruNodes.TryRemove(keyToRemove, out _);
            }

            _cache.TryRemove(keyToRemove, out _);
            Interlocked.Increment(ref _cacheSkips);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposeState, 1) != 0)
        {
            return;
        }

        if (_inner is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync();
        }
        else if (_inner is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}
