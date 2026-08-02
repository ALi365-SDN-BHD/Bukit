using System.Collections.Concurrent;
using Bukit.Engine.Abstractions.Content;
using Xunit;

namespace Bukit.Content.Tests;

public sealed class BodyCacheDecoratorTests
{
    private sealed class CountingBodyStore : IContentBodyStore
    {
        private int _callCount;

        public int CallCount => _callCount;

        public Task<ContentBody> GetAsync(ContentDocument item, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _callCount);
            var html = item.Body.Html ?? $"<p>body-{item.Id}</p>";
            return Task.FromResult(new ContentBody(html));
        }
    }

    private sealed class AsyncDisposableBodyStore : IContentBodyStore, IAsyncDisposable
    {
        private int _disposeCount;

        public int DisposeCount => Volatile.Read(ref _disposeCount);

        public Task<ContentBody> GetAsync(ContentDocument item, CancellationToken cancellationToken = default)
            => Task.FromResult(new ContentBody("<p>body</p>"));

        public ValueTask DisposeAsync()
        {
            Interlocked.Increment(ref _disposeCount);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class DelegatingBodyStore : IContentBodyStore, IAsyncDisposable
    {
        private readonly Func<ContentDocument, CancellationToken, Task<ContentBody>> _getAsync;
        private int _disposeCount;

        public DelegatingBodyStore(Func<ContentDocument, CancellationToken, Task<ContentBody>> getAsync)
        {
            _getAsync = getAsync;
        }

        public int DisposeCount => Volatile.Read(ref _disposeCount);

        public Task<ContentBody> GetAsync(ContentDocument item, CancellationToken cancellationToken = default)
            => _getAsync(item, cancellationToken);

        public ValueTask DisposeAsync()
        {
            Interlocked.Increment(ref _disposeCount);
            return ValueTask.CompletedTask;
        }
    }

    private static ContentDocument CreateItem(string id, string? bodyKey = null, string? contentHtml = null)
    {
        return ContentDocument.Create(
            id: id,
            title: $"Item {id}",
            slug: $"item-{id}",
            publishAt: DateTimeOffset.UtcNow,
            contentHtml: contentHtml,
            fields: null,
            bodyKey: bodyKey);
    }

    [Fact]
    public async Task SameBodyKey_CallsInnerOnlyOnce()
    {
        var inner = new CountingBodyStore();
        var decorator = new BodyCacheDecorator(inner);
        var item = CreateItem("same-key");

        await decorator.GetAsync(item.ToDocument());
        await decorator.GetAsync(item.ToDocument());
        await decorator.GetAsync(item.ToDocument());

        Assert.Equal(1, inner.CallCount);
    }

    [Fact]
    public async Task SameBodyKey_MetricsCorrect()
    {
        var inner = new CountingBodyStore();
        var decorator = new BodyCacheDecorator(inner);
        var item = CreateItem("metrics-test");

        await decorator.GetAsync(item.ToDocument());
        await decorator.GetAsync(item.ToDocument());
        await decorator.GetAsync(item.ToDocument());

        var metrics = decorator.Metrics;
        Assert.Equal(3, metrics.TotalRequests);
        Assert.Equal(2, metrics.CacheHits);
        Assert.Equal(1, metrics.CacheMisses);
        Assert.Equal(0, metrics.InlineBypasses);
        Assert.Equal(1, metrics.UniqueBodies);
        Assert.Equal(3.0, metrics.Amplification);
    }

    [Fact]
    public async Task DifferentBodyKeys_CallInnerSeparately()
    {
        var inner = new CountingBodyStore();
        var decorator = new BodyCacheDecorator(inner);

        await decorator.GetAsync(CreateItem("a").ToDocument());
        await decorator.GetAsync(CreateItem("b").ToDocument());
        await decorator.GetAsync(CreateItem("c").ToDocument());

        Assert.Equal(3, inner.CallCount);
        var metrics = decorator.Metrics;
        Assert.Equal(3, metrics.TotalRequests);
        Assert.Equal(0, metrics.CacheHits);
        Assert.Equal(3, metrics.CacheMisses);
        Assert.Equal(0, metrics.InlineBypasses);
        Assert.Equal(3, metrics.UniqueBodies);
    }

    [Fact]
    public async Task BodyKeyProperty_UsedAsCacheKey()
    {
        var inner = new CountingBodyStore();
        var decorator = new BodyCacheDecorator(inner);
        var item1 = CreateItem("id-1", bodyKey: "shared-key");
        var item2 = CreateItem("id-2", bodyKey: "shared-key");

        await decorator.GetAsync(item1.ToDocument());
        await decorator.GetAsync(item2.ToDocument());

        Assert.Equal(1, inner.CallCount);
    }

    [Fact]
    public async Task ContentHtml_ReturnsImmediately_NoInnerCall()
    {
        var inner = new CountingBodyStore();
        var decorator = new BodyCacheDecorator(inner);
        var item = CreateItem("inline", contentHtml: "<p>inline content</p>");

        await decorator.GetAsync(item.ToDocument());

        Assert.Equal(0, inner.CallCount);
        var metrics = decorator.Metrics;
        Assert.Equal(1, metrics.TotalRequests);
        Assert.Equal(0, metrics.CacheHits);
        Assert.Equal(1, metrics.InlineBypasses);
    }

    [Fact]
    public async Task ConcurrentAccess_SameKey_CallsInnerOnce()
    {
        var inner = new CountingBodyStore();
        var decorator = new BodyCacheDecorator(inner);
        var item = CreateItem("concurrent");

        var tasks = Enumerable.Range(0, 10).Select(_ => decorator.GetAsync(item.ToDocument()));
        await Task.WhenAll(tasks);

        Assert.Equal(1, inner.CallCount);
    }

    [Fact]
    public async Task FailedSharedLoad_IsEvictedAndLaterRequestCanRetry()
    {
        var callCount = 0;
        var inner = new DelegatingBodyStore((_, _) =>
            Interlocked.Increment(ref callCount) == 1
                ? Task.FromException<ContentBody>(new InvalidOperationException("transient"))
                : Task.FromResult(new ContentBody("<p>recovered</p>")));
        var decorator = new BodyCacheDecorator(inner);
        var item = CreateItem("retry-after-fault");

        await Assert.ThrowsAsync<InvalidOperationException>(() => decorator.GetAsync(item.ToDocument()));
        Assert.Equal(0, decorator.Metrics.UniqueBodies);

        ContentBody recovered = await decorator.GetAsync(item.ToDocument());

        Assert.Equal("<p>recovered</p>", recovered.Html);
        Assert.Equal(2, callCount);
    }

    [Fact]
    public async Task FirstCallerCancellation_DoesNotCancelSharedLoadForOtherWaiters()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var callCount = 0;
        var inner = new DelegatingBodyStore(async (_, cancellationToken) =>
        {
            Interlocked.Increment(ref callCount);
            started.TrySetResult();
            await release.Task.WaitAsync(cancellationToken);
            return new ContentBody("<p>shared</p>");
        });
        var decorator = new BodyCacheDecorator(inner);
        var item = CreateItem("independent-waiters");
        using var firstCancellation = new CancellationTokenSource();

        Task<ContentBody> first = decorator.GetAsync(item.ToDocument(), firstCancellation.Token);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Task<ContentBody> second = decorator.GetAsync(item.ToDocument());

        firstCancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);
        release.TrySetResult();

        ContentBody body = await second.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("<p>shared</p>", body.Html);
        Assert.Equal(1, callCount);
    }

    [Fact]
    public async Task DisposeAsync_CancelsAndObservesActiveSharedLoadBeforeDisposingInner()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var inner = new DelegatingBodyStore(async (_, cancellationToken) =>
        {
            started.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new ContentBody("<p>unreachable</p>");
        });
        var decorator = new BodyCacheDecorator(inner);
        Task<ContentBody> activeLoad = decorator.GetAsync(CreateItem("dispose-active").ToDocument());
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await decorator.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => activeLoad.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Equal(1, inner.DisposeCount);
        Assert.Equal(0, decorator.Metrics.UniqueBodies);
    }

    [Fact]
    public async Task LifetimeTokenCancellation_StopsSharedLoad()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var inner = new DelegatingBodyStore(async (_, cancellationToken) =>
        {
            started.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new ContentBody("<p>unreachable</p>");
        });
        using var lifetimeCancellation = new CancellationTokenSource();
        var decorator = new BodyCacheDecorator(inner, 10000, lifetimeCancellation.Token);
        Task<ContentBody> activeLoad = decorator.GetAsync(CreateItem("lifetime-cancel").ToDocument());
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));

        try
        {
            lifetimeCancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => activeLoad.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.Equal(0, decorator.Metrics.UniqueBodies);
        }
        finally
        {
            await decorator.DisposeAsync();
        }
    }

    [Fact]
    public async Task Metrics_UniqueBodies_ReflectsCacheSize()
    {
        var inner = new CountingBodyStore();
        var decorator = new BodyCacheDecorator(inner);

        await Task.WhenAll(decorator.GetAsync(CreateItem("x").ToDocument()), decorator.GetAsync(CreateItem("y").ToDocument()));

        Assert.Equal(2, decorator.Metrics.UniqueBodies);
    }

    [Fact]
    public async Task CacheEvictsOldestEntry_WhenMaxEntriesExceeded()
    {
        var inner = new CountingBodyStore();
        var decorator = new BodyCacheDecorator(inner, maxEntries: 3);

        for (var i = 1; i <= 5; i++)
        {
            await decorator.GetAsync(CreateItem($"item-{i}").ToDocument());
        }

        Assert.True(decorator.Metrics.UniqueBodies <= 3);
    }

    [Fact]
    public async Task CacheEviction_DoesNotAffectMetrics()
    {
        var inner = new CountingBodyStore();
        var decorator = new BodyCacheDecorator(inner, maxEntries: 3);

        for (var i = 1; i <= 5; i++)
        {
            await decorator.GetAsync(CreateItem($"item-{i}").ToDocument());
        }

        var metrics = decorator.Metrics;
        Assert.Equal(5, metrics.TotalRequests);
        Assert.Equal(0, metrics.CacheHits);
        Assert.Equal(5, metrics.CacheMisses);
        Assert.Equal(0, metrics.InlineBypasses);
        Assert.True(metrics.UniqueBodies <= 3);
        Assert.True(metrics.CacheSkips >= 2);
    }

    [Fact]
    public async Task TotalRequests_Equals_CacheHits_Plus_CacheMisses_Plus_InlineBypasses()
    {
        var inner = new CountingBodyStore();
        var decorator = new BodyCacheDecorator(inner);

        await decorator.GetAsync(CreateItem("inline", contentHtml: "<p>inline</p>").ToDocument());
        var item = CreateItem("key-1");
        await decorator.GetAsync(item.ToDocument());
        await decorator.GetAsync(item.ToDocument());

        var m = decorator.Metrics;
        Assert.Equal(m.CacheHits + m.CacheMisses + m.InlineBypasses, m.TotalRequests);
    }

    [Fact]
    public async Task NormalCachePath_HasZero_InlineBypasses()
    {
        var inner = new CountingBodyStore();
        var decorator = new BodyCacheDecorator(inner);

        for (var i = 0; i < 5; i++)
        {
            await decorator.GetAsync(CreateItem($"item-{i}").ToDocument());
        }

        await decorator.GetAsync(CreateItem("item-0").ToDocument());

        var metrics = decorator.Metrics;
        Assert.Equal(0, metrics.InlineBypasses);
        Assert.True(metrics.CacheHits > 0);
    }

    [Fact]
    public async Task LruHitRefreshesEvictionOrder()
    {
        var inner = new CountingBodyStore();
        var decorator = new BodyCacheDecorator(inner, maxEntries: 2);

        var itemA = CreateItem("a");
        var itemB = CreateItem("b");
        var itemC = CreateItem("c");

        await decorator.GetAsync(itemA.ToDocument());
        await decorator.GetAsync(itemB.ToDocument());
        await decorator.GetAsync(itemA.ToDocument());
        await decorator.GetAsync(itemC.ToDocument());

        Assert.Equal(3, inner.CallCount);
        Assert.Equal(2, decorator.Metrics.UniqueBodies);
        Assert.Equal(3, decorator.Metrics.CacheMisses);
        Assert.Equal(1, decorator.Metrics.CacheHits);
        Assert.True(decorator.Metrics.CacheSkips >= 1);

        var hitsBefore = decorator.Metrics.CacheHits;
        var missesBefore = decorator.Metrics.CacheMisses;

        await decorator.GetAsync(itemA.ToDocument());
        Assert.Equal(hitsBefore + 1, decorator.Metrics.CacheHits);
        Assert.Equal(missesBefore, decorator.Metrics.CacheMisses);

        await decorator.GetAsync(itemB.ToDocument());
        Assert.Equal(missesBefore + 1, decorator.Metrics.CacheMisses);
    }

    [Fact]
    public async Task LruEvictsLeastRecentlyUsed()
    {
        var inner = new CountingBodyStore();
        var decorator = new BodyCacheDecorator(inner, maxEntries: 2);

        var itemA = CreateItem("a");
        var itemB = CreateItem("b");
        var itemC = CreateItem("c");

        await decorator.GetAsync(itemA.ToDocument());
        await decorator.GetAsync(itemB.ToDocument());
        await decorator.GetAsync(itemC.ToDocument());

        Assert.Equal(3, inner.CallCount);
        Assert.Equal(2, decorator.Metrics.UniqueBodies);
        Assert.Equal(3, decorator.Metrics.CacheMisses);
        Assert.Equal(0, decorator.Metrics.CacheHits);
        Assert.True(decorator.Metrics.CacheSkips >= 1);

        var hitsBefore = decorator.Metrics.CacheHits;
        var missesBefore = decorator.Metrics.CacheMisses;

        await decorator.GetAsync(itemB.ToDocument());
        Assert.Equal(hitsBefore + 1, decorator.Metrics.CacheHits);
        Assert.Equal(missesBefore, decorator.Metrics.CacheMisses);

        await decorator.GetAsync(itemC.ToDocument());
        Assert.Equal(hitsBefore + 2, decorator.Metrics.CacheHits);

        await decorator.GetAsync(itemA.ToDocument());
        Assert.Equal(missesBefore + 1, decorator.Metrics.CacheMisses);
    }

    [Fact]
    public async Task CompositeSources_SameBodyKey_DoesNotShareCachedBody()
    {
        var bodyStoreA = new CountingBodyStore();
        var bodyStoreB = new CountingBodyStore();

        var itemFromA = CreateItem("index.md", bodyKey: "index.md");
        var itemFromB = CreateItem("index.md", bodyKey: "index.md");

        var decoratorA = new BodyCacheDecorator(bodyStoreA);
        var decoratorB = new BodyCacheDecorator(bodyStoreB);

        var bodyA = await decoratorA.GetAsync(itemFromA.ToDocument());
        var bodyB = await decoratorB.GetAsync(itemFromB.ToDocument());

        Assert.Equal(1, bodyStoreA.CallCount);
        Assert.Equal(1, bodyStoreB.CallCount);
        Assert.NotSame(bodyA, bodyB);
    }

    [Fact]
    public async Task AddToCollections_DuplicatedRoute_SharesSourceBody()
    {
        var inner = new CountingBodyStore();
        var decorator = new BodyCacheDecorator(inner);

        var mainItem = CreateItem("blog:my-post", bodyKey: "blog:my-post");
        var copyItem = CreateItem("blog:my-post:companies", bodyKey: "blog:my-post");

        var mainBody = await decorator.GetAsync(mainItem.ToDocument());
        var copyBody = await decorator.GetAsync(copyItem.ToDocument());

        Assert.Equal(1, inner.CallCount);
        Assert.Equal(mainBody.Html, copyBody.Html);
    }

    [Fact]
    public async Task DisposeAsync_ForwardsToInnerStoreExactlyOnce()
    {
        var inner = new AsyncDisposableBodyStore();
        var decorator = new BodyCacheDecorator(inner);

        var disposable = Assert.IsAssignableFrom<IAsyncDisposable>(decorator);
        await disposable.DisposeAsync();
        await disposable.DisposeAsync();

        Assert.Equal(1, inner.DisposeCount);
    }
}
