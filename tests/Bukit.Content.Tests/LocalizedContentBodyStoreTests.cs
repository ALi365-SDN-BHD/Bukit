using Bukit.Engine.Abstractions.Content;
using Bukit.Content.Media;
using Xunit;

namespace Bukit.Content.Tests;

public sealed class LocalizedContentBodyStoreTests
{
    [Fact]
    public async Task GetAsync_DelegatesToInnerStore()
    {
        var innerCalled = false;
        var inner = new TestBodyStore(item =>
        {
            innerCalled = true;
            return new ContentBody("<p>original</p>");
        });

        var passthroughLocalizer = new TestLocalizer(url => url ?? string.Empty);
        var pipeline = new ContentImageRewritePipeline(
            new Config.MediaConfig { DownloadToLocal = false },
            passthroughLocalizer);

        var store = new LocalizedContentBodyStore(inner, pipeline);
        var item = CreateItem();

        var body = await store.GetAsync(item.ToDocument());

        Assert.True(innerCalled);
        Assert.NotNull(body);
    }

    [Fact]
    public async Task GetAsync_HtmlIsRewrittenThroughPipeline()
    {
        var inner = new TestBodyStore(_ =>
            new ContentBody("<img src=\"https://example.com/img.png\" />"));

        var localizeCalls = new List<string?>();
        var localizer = new TestLocalizer(url =>
        {
            localizeCalls.Add(url);
            return "/localized/img.png";
        });

        var pipeline = new ContentImageRewritePipeline(
            new Config.MediaConfig { DownloadToLocal = false },
            localizer);

        var store = new LocalizedContentBodyStore(inner, pipeline);
        var item = CreateItem();

        var body = await store.GetAsync(item.ToDocument());

        Assert.Contains("/localized/img.png", body.Html);
        Assert.NotEmpty(localizeCalls);
    }

    [Fact]
    public async Task GetAsync_ItemWithoutImages_PassesThroughUnchanged()
    {
        var inner = new TestBodyStore(_ =>
            new ContentBody("<p>No images here</p>"));

        var localizer = new TestLocalizer(url => "/transformed/" + url);
        var pipeline = new ContentImageRewritePipeline(
            new Config.MediaConfig { DownloadToLocal = false },
            localizer);

        var store = new LocalizedContentBodyStore(inner, pipeline);
        var item = CreateItem();

        var body = await store.GetAsync(item.ToDocument());

        Assert.Equal("<p>No images here</p>", body.Html);
    }

    [Fact]
    public async Task GetAsync_NullHtmlFromPipeline_ReturnsEmptyString()
    {
        var inner = new TestBodyStore(_ =>
            new ContentBody("<p>content</p>"));

        var localizer = new TestLocalizer(_ => "/localized/img.png");

        var pipeline = new ContentImageRewritePipeline(
            new Config.MediaConfig { DownloadToLocal = false },
            localizer);

        var store = new LocalizedContentBodyStore(inner, pipeline);
        var item = CreateItem();

        var body = await store.GetAsync(item.ToDocument());

        Assert.NotNull(body);
        Assert.NotNull(body.Html);
    }

    [Fact]
    public async Task GetAsync_CancellationToken_Propagated()
    {
        var inner = new TestBodyStore(_ =>
            new ContentBody("<p>content</p>"));

        var localizer = new TestLocalizer(url => url ?? string.Empty);
        var pipeline = new ContentImageRewritePipeline(
            new Config.MediaConfig { DownloadToLocal = false },
            localizer);

        var store = new LocalizedContentBodyStore(inner, pipeline);
        var item = CreateItem();

        using var cts = new CancellationTokenSource();
        var body = await store.GetAsync(item.ToDocument(), cts.Token);

        Assert.NotNull(body);
    }

    [Fact]
    public async Task DisposeAsync_DisposesOwnedLocalizerExactlyOnce()
    {
        var inner = new TestBodyStore(_ => new ContentBody("<p>content</p>"));
        var localizer = new DisposableTestLocalizer();
        var pipeline = new ContentImageRewritePipeline(
            new Config.MediaConfig { DownloadToLocal = false },
            localizer);
        var store = new LocalizedContentBodyStore(inner, pipeline, localizer);

        await store.DisposeAsync();
        await store.DisposeAsync();

        Assert.Equal(1, localizer.DisposeCount);
    }

    [Fact]
    public async Task DisposeAsync_DisposesOwnedLocalizerAndInnerStoreExactlyOnce()
    {
        var inner = new AsyncDisposableBodyStore();
        var localizer = new DisposableTestLocalizer();
        var pipeline = new ContentImageRewritePipeline(
            new Config.MediaConfig { DownloadToLocal = false },
            localizer);
        var store = new LocalizedContentBodyStore(inner, pipeline, localizer);

        await store.DisposeAsync();
        await store.DisposeAsync();

        Assert.Equal(1, localizer.DisposeCount);
        Assert.Equal(1, inner.DisposeCount);
    }

    [Fact]
    public async Task DisposeAsync_WhenOwnedLocalizerAndInnerStoreAreSameInstance_DisposesOnce()
    {
        var shared = new SharedDisposableStore();
        var pipeline = new ContentImageRewritePipeline(
            new Config.MediaConfig { DownloadToLocal = false },
            shared);
        var store = new LocalizedContentBodyStore(shared, pipeline, shared);

        await store.DisposeAsync();
        await store.DisposeAsync();

        Assert.Equal(1, shared.TotalDisposeCount);
    }

    private static ContentDocument CreateItem()
    {
        return ContentDocument.Create(
            id: "test-1",
            title: "Test",
            slug: "test",
            publishAt: DateTimeOffset.UtcNow,
            contentHtml: null,
            fields: null,
            bodyKey: "test-1");
    }

    private sealed class TestBodyStore : IContentBodyStore
    {
        private readonly Func<ContentDocument, ContentBody> _factory;

        public TestBodyStore(Func<ContentDocument, ContentBody> factory)
        {
            _factory = factory;
        }

        public Task<ContentBody> GetAsync(ContentDocument item, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_factory(item));
        }
    }

    private sealed class TestLocalizer : IImageAssetLocalizer
    {
        private readonly Func<string?, string> _transform;

        public TestLocalizer(Func<string?, string> transform)
        {
            _transform = transform;
        }

        public Task<string> LocalizeAsync(string? sourceUrl, CancellationToken cancellationToken)
        {
            return Task.FromResult(_transform(sourceUrl));
        }
    }

    private sealed class DisposableTestLocalizer : IImageAssetLocalizer, IDisposable
    {
        private int _disposeCount;

        public int DisposeCount => Volatile.Read(ref _disposeCount);

        public Task<string> LocalizeAsync(string? sourceUrl, CancellationToken cancellationToken)
            => Task.FromResult(sourceUrl ?? string.Empty);

        public void Dispose() => Interlocked.Increment(ref _disposeCount);
    }

    private sealed class AsyncDisposableBodyStore : IContentBodyStore, IAsyncDisposable
    {
        private int _disposeCount;

        public int DisposeCount => Volatile.Read(ref _disposeCount);

        public Task<ContentBody> GetAsync(ContentDocument document, CancellationToken cancellationToken = default)
            => Task.FromResult(new ContentBody("<p>content</p>"));

        public ValueTask DisposeAsync()
        {
            Interlocked.Increment(ref _disposeCount);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class SharedDisposableStore : IContentBodyStore, IImageAssetLocalizer, IDisposable, IAsyncDisposable
    {
        private int _totalDisposeCount;

        public int TotalDisposeCount => Volatile.Read(ref _totalDisposeCount);

        public Task<ContentBody> GetAsync(ContentDocument document, CancellationToken cancellationToken = default)
            => Task.FromResult(new ContentBody("<p>content</p>"));

        public Task<string> LocalizeAsync(string? sourceUrl, CancellationToken cancellationToken)
            => Task.FromResult(sourceUrl ?? string.Empty);

        public void Dispose() => Interlocked.Increment(ref _totalDisposeCount);

        public ValueTask DisposeAsync()
        {
            Interlocked.Increment(ref _totalDisposeCount);
            return ValueTask.CompletedTask;
        }
    }
}
