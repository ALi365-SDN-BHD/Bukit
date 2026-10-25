using Bukit.Engine.Abstractions.Content;
using System.Collections.Concurrent;

namespace Bukit.Content.Notion;

internal sealed class NotionBodyStore : IContentBodyStore, IAsyncDisposable
{
    private readonly Func<ContentDocument, CancellationToken, Task<string>> _htmlFactory;
    private readonly ConcurrentDictionary<string, Lazy<Task<ContentBody>>> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly CancellationTokenSource _lifetimeCts;
    private readonly Action? _onCacheEntryPublished;
    private readonly object _admissionLock = new();
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
    {
        ArgumentNullException.ThrowIfNull(htmlFactory);
        _htmlFactory = htmlFactory;
        _lifetimeCts = CancellationTokenSource.CreateLinkedTokenSource(lifetimeToken);
        _onCacheEntryPublished = onCacheEntryPublished;
    }

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
            new Lazy<Task<ContentBody>>(() => Task.FromResult(body), LazyThreadSafetyMode.None));
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
        var lazy = _cache.GetOrAdd(
            key,
            _ => new Lazy<Task<ContentBody>>(
                async () => new ContentBody(await _htmlFactory(document, _lifetimeCts.Token)),
                LazyThreadSafetyMode.ExecutionAndPublication));
        _onCacheEntryPublished?.Invoke();

        Task<ContentBody> sharedTask = lazy.Value;
        try
        {
            return await sharedTask.WaitAsync(cancellationToken);
        }
        catch
        {
            if (sharedTask.IsFaulted || sharedTask.IsCanceled)
            {
                ((ICollection<KeyValuePair<string, Lazy<Task<ContentBody>>>>)_cache)
                    .Remove(new KeyValuePair<string, Lazy<Task<ContentBody>>>(key, lazy));
            }

            throw;
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

        // Wait for accepted renders before cancelling the lifetime token so admitted
        // operations complete against a live store instead of a disposed CTS.
        if (drainTask is not null)
        {
            await drainTask.ConfigureAwait(false);
        }

        _lifetimeCts.Cancel();
        Task[] activeTasks = _cache.Values
            .Where(static lazy => lazy.IsValueCreated)
            .Select(static lazy => (Task)lazy.Value)
            .ToArray();
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
        }
    }
}
