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
    public async Task GetAsync_ConcurrentCallsShareConfiguredDownloadConcurrency()
    {
        var inner = new TestBodyStore(document =>
            new ContentBody(
                $"<img src=\"https://img.example/{document.Id}-a.jpg\" />"
                + $"<img src=\"https://img.example/{document.Id}-b.jpg\" />"));
        var localizer = new BlockingConcurrencyLocalizer(usefulConcurrency: 2);
        var pipeline = new ContentImageRewritePipeline(
            new Config.MediaConfig { MaxConcurrency = 2 },
            localizer);
        var store = new LocalizedContentBodyStore(inner, pipeline);
        var requests = Enumerable.Range(1, 3)
            .Select(index => store.GetAsync(CreateItem(index.ToString(System.Globalization.CultureInfo.InvariantCulture)).ToDocument()))
            .ToArray();

        try
        {
            await localizer.UsefulConcurrencyReached.WaitAsync(TimeSpan.FromSeconds(2));
        }
        finally
        {
            localizer.Release();
        }

        var bodies = await Task.WhenAll(requests).WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(2, localizer.MaxConcurrency);
        Assert.All(bodies, body => Assert.Contains("/localized/", body.Html, StringComparison.Ordinal));
    }

    [Fact]
    public async Task GetAsync_SharedDownloadPermitIsReleasedWhenLocalizerThrows()
    {
        var inner = new TestBodyStore(document =>
            new ContentBody($"<img src=\"https://img.example/{document.Id}.jpg\" />"));
        var localizer = new ThrowFirstBlockingLocalizer();
        var pipeline = new ContentImageRewritePipeline(
            new Config.MediaConfig { MaxConcurrency = 1 },
            localizer);
        var store = new LocalizedContentBodyStore(inner, pipeline);

        var failing = store.GetAsync(CreateItem("failing").ToDocument());
        await localizer.FirstCallStarted.WaitAsync(TimeSpan.FromSeconds(2));
        var succeeding = store.GetAsync(CreateItem("succeeding").ToDocument());
        var callCountBeforeFailure = localizer.CallCount;

        localizer.ThrowFirstCall();
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => failing.WaitAsync(TimeSpan.FromSeconds(2)));
        var body = await succeeding.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(1, callCountBeforeFailure);
        Assert.Equal(2, localizer.CallCount);
        Assert.Contains("/localized/succeeding.jpg", body.Html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetAsync_CancelingSharedDownloadWaitDoesNotLeakPermit()
    {
        var inner = new TestBodyStore(document =>
            new ContentBody($"<img src=\"https://img.example/{document.Id}.jpg\" />"));
        var localizer = new HoldFirstLocalizer();
        var pipeline = new ContentImageRewritePipeline(
            new Config.MediaConfig { MaxConcurrency = 1 },
            localizer);
        var store = new LocalizedContentBodyStore(inner, pipeline);

        var first = store.GetAsync(CreateItem("first").ToDocument());
        await localizer.FirstCallStarted.WaitAsync(TimeSpan.FromSeconds(2));
        using var cancellation = new CancellationTokenSource();
        var canceled = store.GetAsync(CreateItem("canceled").ToDocument(), cancellation.Token);
        var callCountBeforeCancellation = localizer.CallCount;
        cancellation.Cancel();

        try
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => canceled.WaitAsync(TimeSpan.FromSeconds(2)));
        }
        finally
        {
            localizer.ReleaseFirstCall();
        }

        await first.WaitAsync(TimeSpan.FromSeconds(2));
        var subsequent = await store.GetAsync(CreateItem("subsequent").ToDocument())
            .WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(1, callCountBeforeCancellation);
        Assert.Equal(2, localizer.CallCount);
        Assert.Contains("/localized/subsequent.jpg", subsequent.Html, StringComparison.Ordinal);
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

    private static ContentDocument CreateItem(string id = "test-1")
    {
        return ContentDocument.Create(
            id: id,
            title: "Test",
            slug: "test",
            publishAt: DateTimeOffset.UtcNow,
            contentHtml: null,
            fields: null,
            bodyKey: id);
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

    private sealed class BlockingConcurrencyLocalizer : IImageAssetLocalizer
    {
        private readonly int _usefulConcurrency;
        private readonly TaskCompletionSource _usefulConcurrencyReached = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _active;
        private int _maxConcurrency;

        public BlockingConcurrencyLocalizer(int usefulConcurrency)
        {
            _usefulConcurrency = usefulConcurrency;
        }

        public Task UsefulConcurrencyReached => _usefulConcurrencyReached.Task;

        public int MaxConcurrency => Volatile.Read(ref _maxConcurrency);

        public async Task<string> LocalizeAsync(string? sourceUrl, CancellationToken cancellationToken)
        {
            var active = Interlocked.Increment(ref _active);
            UpdateMaxConcurrency(active);
            if (active >= _usefulConcurrency)
            {
                _usefulConcurrencyReached.TrySetResult();
            }

            try
            {
                await _release.Task.WaitAsync(cancellationToken);
                return (sourceUrl ?? string.Empty).Replace(
                    "https://img.example/",
                    "/localized/",
                    StringComparison.Ordinal);
            }
            finally
            {
                Interlocked.Decrement(ref _active);
            }
        }

        public void Release() => _release.TrySetResult();

        private void UpdateMaxConcurrency(int candidate)
        {
            var observed = Volatile.Read(ref _maxConcurrency);
            while (candidate > observed)
            {
                var prior = Interlocked.CompareExchange(ref _maxConcurrency, candidate, observed);
                if (prior == observed)
                {
                    return;
                }

                observed = prior;
            }
        }
    }

    private sealed class ThrowFirstBlockingLocalizer : IImageAssetLocalizer
    {
        private readonly TaskCompletionSource _firstCallStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _throwFirstCall = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _callCount;

        public Task FirstCallStarted => _firstCallStarted.Task;

        public int CallCount => Volatile.Read(ref _callCount);

        public async Task<string> LocalizeAsync(string? sourceUrl, CancellationToken cancellationToken)
        {
            var call = Interlocked.Increment(ref _callCount);
            if (call == 1)
            {
                _firstCallStarted.TrySetResult();
                await _throwFirstCall.Task.WaitAsync(cancellationToken);
                throw new InvalidOperationException("injected localizer failure");
            }

            return (sourceUrl ?? string.Empty).Replace(
                "https://img.example/",
                "/localized/",
                StringComparison.Ordinal);
        }

        public void ThrowFirstCall() => _throwFirstCall.TrySetResult();
    }

    private sealed class HoldFirstLocalizer : IImageAssetLocalizer
    {
        private readonly TaskCompletionSource _firstCallStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _releaseFirstCall = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _callCount;

        public Task FirstCallStarted => _firstCallStarted.Task;

        public int CallCount => Volatile.Read(ref _callCount);

        public async Task<string> LocalizeAsync(string? sourceUrl, CancellationToken cancellationToken)
        {
            var call = Interlocked.Increment(ref _callCount);
            if (call == 1)
            {
                _firstCallStarted.TrySetResult();
                await _releaseFirstCall.Task.WaitAsync(cancellationToken);
            }

            return (sourceUrl ?? string.Empty).Replace(
                "https://img.example/",
                "/localized/",
                StringComparison.Ordinal);
        }

        public void ReleaseFirstCall() => _releaseFirstCall.TrySetResult();
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
