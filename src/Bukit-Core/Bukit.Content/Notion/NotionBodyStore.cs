using Bukit.Engine.Abstractions.Content;
using System.Collections.Concurrent;

namespace Bukit.Content.Notion;

internal sealed class NotionBodyStore : IContentBodyStore, IAsyncDisposable
{
    private readonly Func<ContentDocument, CancellationToken, Task<string>> _htmlFactory;
    private readonly INotionRelationFallbackResolver? _relationFallbackResolver;
    private readonly ConcurrentDictionary<string, Lazy<Task<ContentBody>>> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly CancellationTokenSource _lifetimeCts;
    private readonly Action? _onCacheEntryPublished;
    private readonly object _admissionLock = new();
    private readonly IDisposable? _ownedClient;
    private readonly SemaphoreSlim? _ownedGate;
    private int _activeOperations;
    private bool _admissionClosed;
    private TaskCompletionSource<bool>? _drainTcs;
    private int _disposeState;

    internal NotionBodyStore(Func<ContentDocument, CancellationToken, Task<string>> htmlFactory)
        : this(htmlFactory, CancellationToken.None, onCacheEntryPublished: null)
    {
    }

    internal NotionBodyStore(
        Func<ContentDocument, CancellationToken, Task<string>> htmlFactory,
        CancellationToken lifetimeToken)
        : this(htmlFactory, lifetimeToken, onCacheEntryPublished: null)
    {
    }

    internal NotionBodyStore(
        Func<ContentDocument, CancellationToken, Task<string>> htmlFactory,
        CancellationToken lifetimeToken,
        Action? onCacheEntryPublished)
        : this(htmlFactory, onCacheEntryPublished, ownedClient: null, ownedGate: null, relationFallbackResolver: null, lifetimeToken)
    {
    }

    internal NotionBodyStore(
        Func<ContentDocument, CancellationToken, Task<string>> htmlFactory,
        Action? onCacheEntryPublished,
        IDisposable? ownedClient,
        SemaphoreSlim? ownedGate,
        INotionRelationFallbackResolver? relationFallbackResolver,
        CancellationToken lifetimeToken)
    {
        ArgumentNullException.ThrowIfNull(htmlFactory);
        _htmlFactory = htmlFactory;
        _lifetimeCts = CancellationTokenSource.CreateLinkedTokenSource(lifetimeToken);
        _onCacheEntryPublished = onCacheEntryPublished;
        _ownedClient = ownedClient;
        _ownedGate = ownedGate;
        _relationFallbackResolver = relationFallbackResolver;
    }

    /// <summary>
    /// Relation resolver bound to the same source load (shared client), preferred by
    /// composite projections over creating a new independent resolver.
    /// </summary>
    internal INotionRelationFallbackResolver? RelationFallbackResolver => _relationFallbackResolver;

    internal int CachedEntryCount => _cache.Count;

    /// <summary>
    /// Seeds a completed body result for a key (used by bounded summary prefetch so the
    /// prerendered HTML is fetched exactly once and later reads share the same value).
    /// </summary>
    internal void Seed(string key, ContentBody body)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(body);
        _cache.TryAdd(
            key,
            new Lazy<Task<ContentBody>>(() => Task.FromResult(body), LazyThreadSafetyMode.ExecutionAndPublication));
    }

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
        cancellationToken.ThrowIfCancellationRequested();

        if (!string.IsNullOrWhiteSpace(document.Body.Html))
        {
            return new ContentBody(document.Body.Html);
        }

        var key = document.Body.BodyKey ?? document.Id;
        var staleRetries = 0;
        while (true)
        {
            var createdHere = false;
            var lazy = _cache.GetOrAdd(
                key,
                _ =>
                {
                    createdHere = true;
                    return new Lazy<Task<ContentBody>>(
                        async () => new ContentBody(await _htmlFactory(document, _lifetimeCts.Token)),
                        LazyThreadSafetyMode.ExecutionAndPublication);
                });
            _onCacheEntryPublished?.Invoke();

            var sharedTask = lazy.Value;
            // Acquisition can land in the window where a previous shared task has
            // already completed as failed/cancelled but its continuation has not
            // removed the entry. Such a terminal entry is stale for this read: remove
            // it by identity and acquire a fresh attempt instead of replaying it.
            if (!createdHere && (sharedTask.IsFaulted || sharedTask.IsCanceled) && staleRetries++ < 3)
            {
                if (RemoveEntry(key, lazy))
                {
                    _ = sharedTask.Exception;
                    continue;
                }
            }

            // A shared task may fail or be cancelled after its last waiter already left
            // (waiter cancellation does not stop the shared work). Remove that entry by
            // identity so a later same-key read starts a fresh attempt.
            ObserveSharedTask(key, lazy, sharedTask);
            try
            {
                return await sharedTask.WaitAsync(cancellationToken);
            }
            catch
            {
                if (sharedTask.IsFaulted || sharedTask.IsCanceled)
                {
                    RemoveEntry(key, lazy);
                }

                throw;
            }
        }
    }

    private void ObserveSharedTask(string key, Lazy<Task<ContentBody>> lazy, Task<ContentBody> sharedTask)
    {
        if (sharedTask.IsCompleted)
        {
            if (sharedTask.IsFaulted || sharedTask.IsCanceled)
            {
                RemoveEntry(key, lazy);
                _ = sharedTask.Exception;
            }

            return;
        }

        _ = sharedTask.ContinueWith(
            static (task, state) =>
            {
                var (store, entryKey, entry) = ((NotionBodyStore, string, Lazy<Task<ContentBody>>))state!;
                if (task.IsFaulted || task.IsCanceled)
                {
                    store.RemoveEntry(entryKey, entry);
                    _ = task.Exception;
                }
            },
            (this, key, lazy),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private bool RemoveEntry(string key, Lazy<Task<ContentBody>> lazy)
        => ((ICollection<KeyValuePair<string, Lazy<Task<ContentBody>>>>)_cache)
            .Remove(new KeyValuePair<string, Lazy<Task<ContentBody>>>(key, lazy));

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

        // Wait for accepted renders before cancelling the lifetime token so admitted
        // operations complete against a live store instead of a disposed CTS.
        if (drainTask is not null)
        {
            await drainTask.ConfigureAwait(false);
        }

        _lifetimeCts.Cancel();
        Task[] activeTasks = [.. _cache.Values
            .Where(static lazy => lazy.IsValueCreated)
            .Select(static lazy => (Task)lazy.Value)];
        try
        {
            await Task.WhenAll(activeTasks);
        }
        catch
        {
        }
        finally
        {
            _cache.Clear();
            _lifetimeCts.Dispose();
            // Released only after every admitted/background operation that may use them
            // has settled above.
            _ownedGate?.Dispose();
            _ownedClient?.Dispose();
        }
    }
}
