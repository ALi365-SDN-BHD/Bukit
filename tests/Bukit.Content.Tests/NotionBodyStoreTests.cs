using Bukit.Engine.Abstractions.Content;
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
    public async Task DisposeAsync_CancelsActiveSharedRender()
    {
        var renderStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var store = new NotionBodyStore(async (_, cancellationToken) =>
        {
            renderStarted.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return "<p>unreachable</p>";
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

        var disposable = Assert.IsAssignableFrom<IAsyncDisposable>(store);
        await disposable.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => activeRender.WaitAsync(TimeSpan.FromSeconds(5)));
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
}
