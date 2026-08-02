using Bukit.Engine.Abstractions.Content;
using System.Collections.Concurrent;

namespace Bukit.Content.Notion;

internal sealed class NotionBodyStore : IContentBodyStore, IAsyncDisposable
{
    private readonly Func<ContentDocument, CancellationToken, Task<string>> _htmlFactory;
    private readonly ConcurrentDictionary<string, Lazy<Task<ContentBody>>> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly CancellationTokenSource _lifetimeCts;
    private int _disposeState;

    internal NotionBodyStore(Func<ContentDocument, CancellationToken, Task<string>> htmlFactory)
        : this(htmlFactory, CancellationToken.None)
    {
    }

    internal NotionBodyStore(
        Func<ContentDocument, CancellationToken, Task<string>> htmlFactory,
        CancellationToken lifetimeToken)
    {
        ArgumentNullException.ThrowIfNull(htmlFactory);
        _htmlFactory = htmlFactory;
        _lifetimeCts = CancellationTokenSource.CreateLinkedTokenSource(lifetimeToken);
    }

    public async Task<ContentBody> GetAsync(ContentDocument document, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposeState) != 0, this);
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

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposeState, 1) != 0)
        {
            return;
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
