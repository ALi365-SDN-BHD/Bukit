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
    private readonly CancellationTokenSource _lifetimeCts;
    private readonly Action? _onCacheEntryPublishedBeforeLru;

    private readonly object _admissionLock = new();
    private int _activeOperations;
    private bool _admissionClosed;
    private TaskCompletionSource<bool>? _drainTcs;

    private long _totalRequests;
    private long _cacheHits;
    private long _cacheMisses;
    private long _inlineBypasses;
    private long _cacheSkips;
    private int _disposeState;

    public BodyCacheDecorator(IContentBodyStore inner, int maxEntries = 10000)
        : this(inner, maxEntries, CancellationToken.None)
    {
    }

    public BodyCacheDecorator(
        IContentBodyStore inner,
        int maxEntries,
        CancellationToken lifetimeToken)
        : this(inner, maxEntries, lifetimeToken, onCacheEntryPublishedBeforeLru: null)
    {
    }

    internal BodyCacheDecorator(
        IContentBodyStore inner,
        int maxEntries,
        CancellationToken lifetimeToken,
        Action? onCacheEntryPublishedBeforeLru)
    {
        ArgumentNullException.ThrowIfNull(inner);
        _inner = inner;
        _maxEntries = maxEntries > 0 ? maxEntries : 10000;
        _lifetimeCts = CancellationTokenSource.CreateLinkedTokenSource(lifetimeToken);
        _onCacheEntryPublishedBeforeLru = onCacheEntryPublishedBeforeLru;
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
        EnterOperation();
        try
        {
            return await GetAsyncCore(document, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            ExitOperation();
        }
    }

    private async Task<ContentBody> GetAsyncCore(ContentDocument document, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _totalRequests);
        cancellationToken.ThrowIfCancellationRequested();

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
            return await AwaitSharedBodyAsync(key, lazy, cancellationToken);
        }

        var newLazy = new Lazy<Task<ContentBody>>(
            async () => new ContentBody((await _inner.GetAsync(document, _lifetimeCts.Token)).Html),
            LazyThreadSafetyMode.ExecutionAndPublication);
        lazy = _cache.GetOrAdd(key, newLazy);
        if (ReferenceEquals(lazy, newLazy))
        {
            Interlocked.Increment(ref _cacheMisses);
            _onCacheEntryPublishedBeforeLru?.Invoke();
            lock (_lruLock)
            {
                if (_cache.TryGetValue(key, out var publishedLazy)
                    && ReferenceEquals(publishedLazy, lazy))
                {
                    var node = _lruList.AddLast(key);
                    _lruNodes[key] = node;
                }
            }
            TrimExcess();
        }
        else
        {
            Interlocked.Increment(ref _cacheHits);
        }

        return await AwaitSharedBodyAsync(key, lazy, cancellationToken);
    }

    private async Task<ContentBody> AwaitSharedBodyAsync(
        string key,
        Lazy<Task<ContentBody>> lazy,
        CancellationToken cancellationToken)
    {
        Task<ContentBody> sharedTask = lazy.Value;
        try
        {
            return await sharedTask.WaitAsync(cancellationToken);
        }
        catch
        {
            if (sharedTask.IsFaulted || sharedTask.IsCanceled)
            {
                RemoveCacheEntry(key, lazy);
            }

            throw;
        }
    }

    private void RemoveCacheEntry(string key, Lazy<Task<ContentBody>> lazy)
    {
        lock (_lruLock)
        {
            if (!((ICollection<KeyValuePair<string, Lazy<Task<ContentBody>>>>)_cache)
                    .Remove(new KeyValuePair<string, Lazy<Task<ContentBody>>>(key, lazy)))
            {
                return;
            }

            if (_lruNodes.TryRemove(key, out var node))
            {
                _lruList.Remove(node);
            }
        }
    }

    private void TrimExcess()
    {
        // Remove exactly the current excess; the count is re-checked under the LRU lock
        // on every iteration so concurrent insertions never trigger bulk eviction.
        while (true)
        {
            lock (_lruLock)
            {
                if (_cache.Count <= _maxEntries || _lruList.First is null)
                {
                    return;
                }

                var keyToRemove = _lruList.First.Value;
                _lruList.RemoveFirst();
                _lruNodes.TryRemove(keyToRemove, out _);
                _cache.TryRemove(keyToRemove, out _);
            }

            Interlocked.Increment(ref _cacheSkips);
        }
    }

    private void EnterOperation()
    {
        lock (_admissionLock)
        {
            if (_admissionClosed || Volatile.Read(ref _disposeState) != 0)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            _activeOperations++;
        }
    }

    private void ExitOperation()
    {
        lock (_admissionLock)
        {
            _activeOperations--;
            if (_activeOperations == 0 && _admissionClosed)
            {
                _drainTcs?.TrySetResult(true);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposeState, 1) != 0)
        {
            return;
        }

        Task? drainTask;
        lock (_admissionLock)
        {
            _admissionClosed = true;
            if (_activeOperations > 0)
            {
                _drainTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                drainTask = _drainTcs.Task;
            }
            else
            {
                drainTask = null;
            }
        }

        // Disposal must not reach the inner store while an accepted GetAsync is still
        // running; admitted operations complete against a live store.
        if (drainTask is not null)
        {
            await drainTask.ConfigureAwait(false);
        }

        _lifetimeCts.Cancel();
        Task[] activeTasks = _cache.Values
            .Where(static lazy => lazy.IsValueCreated)
            .Select(static lazy => (Task)lazy.Value)
            .ToArray();
        await ObserveTasksAsync(activeTasks);

        _cache.Clear();
        lock (_lruLock)
        {
            _lruList.Clear();
            _lruNodes.Clear();
        }

        try
        {
            if (_inner is IAsyncDisposable asyncDisposable)
            {
                await asyncDisposable.DisposeAsync();
            }
            else if (_inner is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
        finally
        {
            _lifetimeCts.Dispose();
        }
    }

    private static async Task ObserveTasksAsync(IReadOnlyList<Task> tasks)
    {
        try
        {
            await Task.WhenAll(tasks);
        }
        catch
        {
        }
    }
}
