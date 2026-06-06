using Bukit.Engine.Abstractions.Content;
using Xunit;

namespace Bukit.Content.Tests;

public sealed class EmptyContentBodyStoreTests
{
    [Fact]
    public async Task GetAsync_HasContentHtml_ReturnsInline()
    {
        var item = new ContentItem(
            Id: "inline", Title: "Inline", Slug: "inline",
            PublishAt: default, ContentHtml: "<p>inline</p>");

        var body = await EmptyContentBodyStore.Instance.GetAsync(item);

        Assert.Equal("<p>inline</p>", body.Html);
    }

    [Fact]
    public async Task GetAsync_NoContentHtml_Throws()
    {
        var item = new ContentItem(
            Id: "empty", Title: "Empty", Slug: "empty",
            PublishAt: default, ContentHtml: null);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => EmptyContentBodyStore.Instance.GetAsync(item));

        Assert.Contains("No content body available", ex.Message);
    }

    [Fact]
    public async Task GetAsync_Cancellation_Throws()
    {
        var item = new ContentItem(
            Id: "cancelled", Title: "Cancelled", Slug: "cancelled",
            PublishAt: default, ContentHtml: "<p>ok</p>");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => EmptyContentBodyStore.Instance.GetAsync(item, cts.Token));
    }
}
