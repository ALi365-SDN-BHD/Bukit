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
