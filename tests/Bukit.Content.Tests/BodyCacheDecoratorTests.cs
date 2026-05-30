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

        public Task<ContentBody> GetAsync(ContentItem item, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _callCount);
            var html = item.ContentHtml ?? $"<p>body-{item.Id}</p>";
            return Task.FromResult(new ContentBody(html));
        }
    }

    private static ContentItem CreateItem(string id, string? bodyKey = null, string? contentHtml = null)
    {
        return new ContentItem(
            Id: id,
            Title: $"Item {id}",
            Slug: $"item-{id}",
            PublishAt: DateTimeOffset.UtcNow,
            ContentHtml: contentHtml,
            Meta: new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase),
            Fields: null,
            BodyKey: bodyKey);
    }

    [Fact]
    public async Task SameBodyKey_CallsInnerOnlyOnce()
    {
        var inner = new CountingBodyStore();
        var decorator = new BodyCacheDecorator(inner);
        var item = CreateItem("same-key");

        await decorator.GetAsync(item);
        await decorator.GetAsync(item);
        await decorator.GetAsync(item);

        Assert.Equal(1, inner.CallCount);
    }

    [Fact]
    public async Task SameBodyKey_MetricsCorrect()
    {
        var inner = new CountingBodyStore();
        var decorator = new BodyCacheDecorator(inner);
        var item = CreateItem("metrics-test");

        await decorator.GetAsync(item);
        await decorator.GetAsync(item);
        await decorator.GetAsync(item);

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

        await decorator.GetAsync(CreateItem("a"));
        await decorator.GetAsync(CreateItem("b"));
        await decorator.GetAsync(CreateItem("c"));

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

        await decorator.GetAsync(item1);
        await decorator.GetAsync(item2);

        Assert.Equal(1, inner.CallCount);
    }

    [Fact]
    public async Task ContentHtml_ReturnsImmediately_NoInnerCall()
    {
        var inner = new CountingBodyStore();
        var decorator = new BodyCacheDecorator(inner);
        var item = CreateItem("inline", contentHtml: "<p>inline content</p>");

        await decorator.GetAsync(item);

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

        var tasks = Enumerable.Range(0, 10).Select(_ => decorator.GetAsync(item));
        await Task.WhenAll(tasks);

        Assert.Equal(1, inner.CallCount);
    }

    [Fact]
    public async Task Metrics_UniqueBodies_ReflectsCacheSize()
    {
        var inner = new CountingBodyStore();
        var decorator = new BodyCacheDecorator(inner);

        await Task.WhenAll(decorator.GetAsync(CreateItem("x")), decorator.GetAsync(CreateItem("y")));

        Assert.Equal(2, decorator.Metrics.UniqueBodies);
    }

    [Fact]
    public async Task CacheEvictsOldestEntry_WhenMaxEntriesExceeded()
    {
        var inner = new CountingBodyStore();
        var decorator = new BodyCacheDecorator(inner, maxEntries: 3);

        for (var i = 1; i <= 5; i++)
        {
            await decorator.GetAsync(CreateItem($"item-{i}"));
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
            await decorator.GetAsync(CreateItem($"item-{i}"));
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

        await decorator.GetAsync(CreateItem("inline", contentHtml: "<p>inline</p>"));
        var item = CreateItem("key-1");
        await decorator.GetAsync(item);
        await decorator.GetAsync(item);

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
            await decorator.GetAsync(CreateItem($"item-{i}"));
        }

        await decorator.GetAsync(CreateItem("item-0"));

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

        await decorator.GetAsync(itemA);
        await decorator.GetAsync(itemB);
        await decorator.GetAsync(itemA);
        await decorator.GetAsync(itemC);

        Assert.Equal(3, inner.CallCount);
        Assert.Equal(2, decorator.Metrics.UniqueBodies);
        Assert.Equal(3, decorator.Metrics.CacheMisses);
        Assert.Equal(1, decorator.Metrics.CacheHits);
        Assert.True(decorator.Metrics.CacheSkips >= 1);

        var hitsBefore = decorator.Metrics.CacheHits;
        var missesBefore = decorator.Metrics.CacheMisses;

        await decorator.GetAsync(itemA);
        Assert.Equal(hitsBefore + 1, decorator.Metrics.CacheHits);
        Assert.Equal(missesBefore, decorator.Metrics.CacheMisses);

        await decorator.GetAsync(itemB);
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

        await decorator.GetAsync(itemA);
        await decorator.GetAsync(itemB);
        await decorator.GetAsync(itemC);

        Assert.Equal(3, inner.CallCount);
        Assert.Equal(2, decorator.Metrics.UniqueBodies);
        Assert.Equal(3, decorator.Metrics.CacheMisses);
        Assert.Equal(0, decorator.Metrics.CacheHits);
        Assert.True(decorator.Metrics.CacheSkips >= 1);

        var hitsBefore = decorator.Metrics.CacheHits;
        var missesBefore = decorator.Metrics.CacheMisses;

        await decorator.GetAsync(itemB);
        Assert.Equal(hitsBefore + 1, decorator.Metrics.CacheHits);
        Assert.Equal(missesBefore, decorator.Metrics.CacheMisses);

        await decorator.GetAsync(itemC);
        Assert.Equal(hitsBefore + 2, decorator.Metrics.CacheHits);

        await decorator.GetAsync(itemA);
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

        var bodyA = await decoratorA.GetAsync(itemFromA);
        var bodyB = await decoratorB.GetAsync(itemFromB);

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

        var mainBody = await decorator.GetAsync(mainItem);
        var copyBody = await decorator.GetAsync(copyItem);

        Assert.Equal(1, inner.CallCount);
        Assert.Equal(mainBody.Html, copyBody.Html);
    }
}
