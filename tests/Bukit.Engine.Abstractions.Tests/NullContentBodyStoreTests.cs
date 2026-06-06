using Bukit.Engine.Abstractions.Content;
using Xunit;

namespace Bukit.Engine.Abstractions.Tests;

public class NullContentBodyStoreTests
{
    [Fact]
    public void Instance_SameObject()
    {
        Assert.Same(NullContentBodyStore.Instance, NullContentBodyStore.Instance);
    }

    [Fact]
    public async Task GetAsync_WithContent_ReturnsContentBody()
    {
        var store = NullContentBodyStore.Instance;
        var item = new ContentItem(
            "test-id",
            "Test Title",
            "test-slug",
            DateTimeOffset.UtcNow,
            "<p>hello</p>",
            null);

        var body = await store.GetAsync(item);

        Assert.NotNull(body);
        Assert.Equal("<p>hello</p>", body.Html);
    }

    [Fact]
    public async Task GetAsync_WithoutContent_Throws()
    {
        var store = NullContentBodyStore.Instance;
        var item = new ContentItem(
            "test-id",
            "Test Title",
            "test-slug",
            DateTimeOffset.UtcNow,
            null,
            null);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => store.GetAsync(item));

        Assert.Contains("test-id", ex.Message);
    }

    [Fact]
    public async Task GetAsync_Cancelled_Throws()
    {
        var store = NullContentBodyStore.Instance;
        var item = new ContentItem(
            "test-id",
            "Test Title",
            "test-slug",
            DateTimeOffset.UtcNow,
            "<p>hello</p>",
            null);

        var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => store.GetAsync(item, cts.Token));
    }
}
