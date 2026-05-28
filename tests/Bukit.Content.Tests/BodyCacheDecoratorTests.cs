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
        Assert.Equal(3, metrics.CacheHits);
        Assert.Equal(1, metrics.CacheMisses);
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
        Assert.Equal(3, metrics.CacheMisses);
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
}
