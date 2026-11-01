using Bukit.Engine.Abstractions.Content;
using System.Reflection;
using Bukit.Content.Notion;
using Xunit;

namespace Bukit.Content.Tests;

public sealed class NotionBodyStoreTests
{
    [Fact]
    public async Task GetAsync_RendersOnDemand_AndCachesByBodyKey()
    {
        var renderCount = 0;
        var store = new NotionBodyStore(async (item, cancellationToken) =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            renderCount++;
            await Task.Yield();
            return $"<p>{item.Id}</p>";
        });

        var item = ContentDocument.Create(
            id: "page-1",
            title: "Page",
            slug: "page",
            publishAt: DateTimeOffset.UtcNow,
            contentHtml: null,
            fields: null,
            bodyKey: "page-1");

        Assert.Equal(0, renderCount);

        var first = await store.GetAsync(item.ToDocument());
        var second = await store.GetAsync(item.ToDocument());

        Assert.Equal("<p>page-1</p>", first.Html);
        Assert.Equal("<p>page-1</p>", second.Html);
        Assert.Equal(1, renderCount);
    }

    [Fact]
    public async Task GetAsync_WithInlineContentHtml_ReturnsInlineBodyWithoutCallingFactory()
    {
        var invoked = false;
        var store = new NotionBodyStore((_, _) =>
        {
            invoked = true;
            return Task.FromResult("<p>should not render</p>");
        });

        var item = ContentDocument.Create(
            id: "page-1",
            title: "Page",
            slug: "page",
            publishAt: DateTimeOffset.UtcNow,
            contentHtml: "<p>inline content</p>",
            fields: null,
            bodyKey: "page-1");

        var body = await store.GetAsync(item.ToDocument());

        Assert.Equal("<p>inline content</p>", body.Html);
        Assert.False(invoked);
    }

    [Fact]
    public async Task GetAsync_FirstCallerCancellation_DoesNotCancelSharedRender()
    {
        var renderStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseRender = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var renderCount = 0;
        var store = new NotionBodyStore(async (_, cancellationToken) =>
        {
            Interlocked.Increment(ref renderCount);
            renderStarted.TrySetResult();
            await releaseRender.Task.WaitAsync(cancellationToken);
            return "<p>success</p>";
        });
        var item = ContentDocument.Create(
            id: "page",
            title: "Page",
            slug: "page",
            publishAt: DateTimeOffset.UtcNow,
            contentHtml: null,
            fields: null,
            bodyKey: "page");
        using var cancellation = new CancellationTokenSource();

        var firstRequest = store.GetAsync(item.ToDocument(), cancellation.Token);
        await renderStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var secondRequest = store.GetAsync(item.ToDocument());
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => firstRequest);
        releaseRender.TrySetResult();

        var second = await secondRequest.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal("<p>success</p>", second.Html);
        Assert.Equal(1, renderCount);
    }

    [Fact]
    public async Task DisposeAsync_WaitsForAdmittedSharedRenderBeforeCancellation()
    {
        var renderStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var store = new NotionBodyStore(async (_, cancellationToken) =>
        {
            renderStarted.TrySetResult();
            await release.Task.WaitAsync(TimeSpan.FromSeconds(10));
            return "<p>admitted</p>";
        });
        var item = ContentDocument.Create(
            id: "dispose-page",
            title: "Page",
            slug: "dispose-page",
            publishAt: DateTimeOffset.UtcNow,
            contentHtml: null,
            fields: null,
            bodyKey: "dispose-page");
        Task<ContentBody> activeRender = store.GetAsync(item.ToDocument());
        await renderStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Admission-gate contract: disposal drains the accepted render before the
        // lifetime token is cancelled.
        var disposable = Assert.IsAssignableFrom<IAsyncDisposable>(store);
        var disposeTask = disposable.DisposeAsync().AsTask();
        var winner = await Task.WhenAny(disposeTask, Task.Delay(250));
        Assert.NotSame(disposeTask, winner);

        release.SetResult();
        var body = await activeRender.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("<p>admitted</p>", body.Html);

        await disposeTask.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task GetAsync_AfterDispose_ThrowsObjectDisposedBeforeInlineBypass()
    {
        var store = new NotionBodyStore((_, _) => Task.FromResult("<p>factory</p>"));
        await store.DisposeAsync();
        var item = ContentDocument.Create(
            id: "disposed-page",
            title: "Page",
            slug: "disposed-page",
            publishAt: DateTimeOffset.UnixEpoch,
            contentHtml: "<p>inline</p>",
            fields: null,
            bodyKey: "disposed-page");

        await Assert.ThrowsAsync<ObjectDisposedException>(() => store.GetAsync(item.ToDocument()));
    }

    [Fact]
    public async Task GetAsync_UniqueWaiterCancelThenDelayedFailure_NextReadStartsFreshAttempt()
    {
        var firstStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var renderCount = 0;
        var store = new NotionBodyStore(async (_, _) =>
        {
            if (Interlocked.Increment(ref renderCount) == 1)
            {
                firstStarted.TrySetResult();
                await releaseFirst.Task;
                throw new InvalidOperationException("delayed failure");
            }

            return "<p>fresh</p>";
        });
        var item = ContentDocument.Create(
            id: "retry-page",
            title: "Page",
            slug: "retry-page",
            publishAt: DateTimeOffset.UtcNow,
            contentHtml: null,
            fields: null,
            bodyKey: "retry-page");
        using var cancellation = new CancellationTokenSource();

        var firstRequest = store.GetAsync(item.ToDocument(), cancellation.Token);
        await firstStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => firstRequest);
        releaseFirst.TrySetResult();

        for (var attempt = 0; attempt < 200 && store.CachedEntryCount != 0; attempt++)
        {
            await Task.Delay(10);
        }

        Assert.Equal(0, store.CachedEntryCount);
        var second = await store.GetAsync(item.ToDocument()).WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal("<p>fresh</p>", second.Html);
        Assert.Equal(2, renderCount);
    }

    [Fact]
    public async Task GetAsync_TerminalFailedEntryBeforeEviction_StartsFreshAttempt()
    {
        var calls = 0;
        var store = new NotionBodyStore((_, _) =>
        {
            Interlocked.Increment(ref calls);
            return Task.FromResult("<p>fresh</p>");
        });
        var item = Item("terminal-fault");
        PublishTerminalEntry(store, "terminal-fault",
            new Lazy<Task<ContentBody>>(
                () => Task.FromException<ContentBody>(new InvalidOperationException("stale failure")),
                LazyThreadSafetyMode.ExecutionAndPublication));

        var body = await store.GetAsync(item);

        Assert.Equal("<p>fresh</p>", body.Html);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task GetAsync_TerminalCanceledEntryBeforeEviction_StartsFreshAttempt()
    {
        var calls = 0;
        var store = new NotionBodyStore((_, _) =>
        {
            Interlocked.Increment(ref calls);
            return Task.FromResult("<p>fresh</p>");
        });
        var item = Item("terminal-cancel");
        PublishTerminalEntry(store, "terminal-cancel",
            new Lazy<Task<ContentBody>>(
                () => Task.FromCanceled<ContentBody>(new CancellationToken(canceled: true)),
                LazyThreadSafetyMode.ExecutionAndPublication));

        var body = await store.GetAsync(item);

        Assert.Equal("<p>fresh</p>", body.Html);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task GetAsync_OldCleanupCannotRemoveNewSuccessfulEntry()
    {
        var calls = 0;
        var store = new NotionBodyStore((_, _) =>
        {
            Interlocked.Increment(ref calls);
            return Task.FromResult("<p>fresh</p>");
        });
        var item = Item("identity");
        var stale = new Lazy<Task<ContentBody>>(
            () => Task.FromException<ContentBody>(new InvalidOperationException("stale")),
            LazyThreadSafetyMode.ExecutionAndPublication);
        PublishTerminalEntry(store, "identity", stale);

        await store.GetAsync(item);
        var removeEntry = typeof(NotionBodyStore).GetMethod("RemoveEntry", BindingFlags.Instance | BindingFlags.NonPublic)!;
        removeEntry.Invoke(store, ["identity", stale]);

        var body = await store.GetAsync(item);

        Assert.Equal("<p>fresh</p>", body.Html);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task GetAsync_ConcurrentFirstReadsOfSeededEntry_AllReturnSeedWithoutFactory()
    {
        var calls = 0;
        var store = new NotionBodyStore((_, _) =>
        {
            Interlocked.Increment(ref calls);
            return Task.FromResult("<p>factory</p>");
        });
        store.Seed("seeded", new ContentBody("<p>seed</p>"));
        var item = Item("seeded");

        var reads = Enumerable.Range(0, 512)
            .Select(_ => Task.Run(() => store.GetAsync(item)))
            .ToArray();
        var bodies = await Task.WhenAll(reads).WaitAsync(TimeSpan.FromSeconds(10));

        Assert.All(bodies, body => Assert.Equal("<p>seed</p>", body.Html));
        Assert.Equal(0, calls);
    }

    private static ContentDocument Item(string key)
        => ContentDocument.Create(
            id: key,
            title: "Page",
            slug: key,
            publishAt: DateTimeOffset.UtcNow,
            contentHtml: null,
            fields: null,
            bodyKey: key).ToDocument();

    private static void PublishTerminalEntry(NotionBodyStore store, string key, Lazy<Task<ContentBody>> lazy)
    {
        _ = lazy.Value;
        var cache = (IDictionary<string, Lazy<Task<ContentBody>>>)typeof(NotionBodyStore)
            .GetField("_cache", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(store)!;
        cache[key] = lazy;
    }

    [Fact]
    public async Task GetAsync_FailedSharedTaskWithoutWaiters_DoesNotPoisonLaterSuccess()
    {
        var attempt = 0;
        var store = new NotionBodyStore((_, _) =>
        {
            if (Interlocked.Increment(ref attempt) == 1)
            {
                return Task.FromException<string>(new InvalidOperationException("first attempt"));
            }

            return Task.FromResult("<p>second</p>");
        });
        var item = ContentDocument.Create(
            id: "poison-page",
            title: "Page",
            slug: "poison-page",
            publishAt: DateTimeOffset.UtcNow,
            contentHtml: null,
            fields: null,
            bodyKey: "poison-page");

        await Assert.ThrowsAsync<InvalidOperationException>(() => store.GetAsync(item.ToDocument()));

        Assert.Equal(0, store.CachedEntryCount);
        var body = await store.GetAsync(item.ToDocument());
        Assert.Equal("<p>second</p>", body.Html);
        Assert.Equal(2, attempt);
    }

    [Fact]
    public async Task DisposeAsync_WithOwnedClientAndGate_ReleasesThemOnlyAfterAcceptedRenderCompletes()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var ownedClient = new CountingDisposable();
        var ownedGate = new SemaphoreSlim(2, 2);
        var store = new NotionBodyStore(
            async (_, cancellationToken) =>
            {
                started.TrySetResult();
                await release.Task;
                return "<p>active</p>";
            },
            onCacheEntryPublished: null,
            ownedClient,
            ownedGate,
            relationFallbackResolver: null,
            lifetimeToken: CancellationToken.None);
        var item = Item("owned-active");

        var read = store.GetAsync(item);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var dispose = store.DisposeAsync().AsTask();
        await Task.Delay(100);

        Assert.Equal(0, ownedClient.DisposeCount);
        Assert.False(dispose.IsCompleted);
        release.TrySetResult();
        Assert.Equal("<p>active</p>", (await read.WaitAsync(TimeSpan.FromSeconds(5))).Html);
        await dispose.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(1, ownedClient.DisposeCount);
        Assert.Throws<ObjectDisposedException>(() => ownedGate.Wait(0));
    }

    private sealed class CountingDisposable : IDisposable
    {
        private int _disposeCount;
        public int DisposeCount => Volatile.Read(ref _disposeCount);
        public void Dispose() => Interlocked.Increment(ref _disposeCount);
    }

    [Fact]
    public async Task NotionGetAsync_AdmittedBeforeDispose_CompletesBeforeCtsDisposal()
    {
        var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        var store = new NotionBodyStore(
            async (item, cancellationToken) =>
            {
                entered.TrySetResult(true);
                await release.Task;
                // An accepted render must finish: disposal may not cancel the lifetime
                // before the admitted operation completes.
                cancellationToken.ThrowIfCancellationRequested();
                return $"<p>{item.Id}</p>";
            },
            CancellationToken.None,
            onCacheEntryPublished: null);

        var item = ContentDocument.Create(
            id: "admitted-page",
            title: "Page",
            slug: "page",
            publishAt: DateTimeOffset.UtcNow,
            contentHtml: null,
            fields: null,
            bodyKey: "admitted-page");

        var getTask = store.GetAsync(item.ToDocument());
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var disposeTask = store.DisposeAsync().AsTask();
        var winner = await Task.WhenAny(disposeTask, Task.Delay(250));
        Assert.NotSame(disposeTask, winner);

        release.SetResult(true);
        var body = await getTask.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("<p>admitted-page</p>", body.Html);

        await disposeTask.WaitAsync(TimeSpan.FromSeconds(5));
    }
}
