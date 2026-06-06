using Bukit.Engine.Abstractions.Content;
using Xunit;

namespace Bukit.Content.Tests;

public sealed class EmptyContentBodyStoreTests
{
    [Fact]
    public async Task GetAsync_HasContentHtml_ReturnsInline()
    {
        var item = ContentDocument.Create(
            id: "inline", title: "Inline", slug: "inline",
            publishAt: default, contentHtml: "<p>inline</p>",
            fields: ContentFieldReader.ToFieldMap(new Dictionary<string, object>()));

        var body = await EmptyContentBodyStore.Instance.GetAsync(item.ToDocument());

        Assert.Equal("<p>inline</p>", body.Html);
    }

    [Fact]
    public async Task GetAsync_NoContentHtml_Throws()
    {
        var item = ContentDocument.Create(
            id: "empty", title: "Empty", slug: "empty",
            publishAt: default, contentHtml: null,
            fields: ContentFieldReader.ToFieldMap(new Dictionary<string, object>()));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => EmptyContentBodyStore.Instance.GetAsync(item.ToDocument()));

        Assert.Contains("No content body available", ex.Message);
    }

    [Fact]
    public async Task GetAsync_Cancellation_Throws()
    {
        var item = ContentDocument.Create(
            id: "cancelled", title: "Cancelled", slug: "cancelled",
            publishAt: default, contentHtml: "<p>ok</p>",
            fields: ContentFieldReader.ToFieldMap(new Dictionary<string, object>()));
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => EmptyContentBodyStore.Instance.GetAsync(item.ToDocument(), cts.Token));
    }
}
