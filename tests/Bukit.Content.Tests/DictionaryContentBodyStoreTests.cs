using Bukit.Engine.Abstractions.Content;
using Xunit;

namespace Bukit.Content.Tests;

public sealed class DictionaryContentBodyStoreTests
{
    [Fact]
    public async Task GetAsync_ExistingBodyKey_ReturnsBody()
    {
        var body = new ContentBody("<p>hello</p>");
        var store = new DictionaryContentBodyStore(new Dictionary<string, ContentBody>
        {
            ["post-1"] = body
        });
        var item = new ContentItem(
            Id: "post-1", Title: "Post", Slug: "post",
            PublishAt: default, ContentHtml: null,
            Meta: new Dictionary<string, object>(),
            BodyKey: "post-1");

        var result = await store.GetAsync(item);

        Assert.Same(body, result);
    }

    [Fact]
    public async Task GetAsync_HasContentHtml_ReturnsInline()
    {
        var store = new DictionaryContentBodyStore(new Dictionary<string, ContentBody>());
        var item = new ContentItem(
            Id: "inline", Title: "Inline", Slug: "inline",
            PublishAt: default, ContentHtml: "<p>inline</p>",
            Meta: new Dictionary<string, object>());

        var body = await store.GetAsync(item);

        Assert.Equal("<p>inline</p>", body.Html);
    }

    [Fact]
    public async Task GetAsync_EmptyBodyKey_Throws()
    {
        var store = new DictionaryContentBodyStore(new Dictionary<string, ContentBody>());
        var item = new ContentItem(
            Id: "missing", Title: "Missing", Slug: "missing",
            PublishAt: default, ContentHtml: null,
            Meta: new Dictionary<string, object>(),
            BodyKey: "");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => store.GetAsync(item));
        Assert.Contains("No content body found", ex.Message);
    }

    [Fact]
    public async Task GetAsync_NullBodyKey_Throws()
    {
        var store = new DictionaryContentBodyStore(new Dictionary<string, ContentBody>());
        var item = new ContentItem(
            Id: "missing", Title: "Missing", Slug: "missing",
            PublishAt: default, ContentHtml: null,
            Meta: new Dictionary<string, object>(),
            BodyKey: null);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => store.GetAsync(item));
        Assert.Contains("No content body found", ex.Message);
    }

    [Fact]
    public async Task GetAsync_MissingBodyKey_Throws()
    {
        var store = new DictionaryContentBodyStore(new Dictionary<string, ContentBody>());
        var item = new ContentItem(
            Id: "missing", Title: "Missing", Slug: "missing",
            PublishAt: default, ContentHtml: null,
            Meta: new Dictionary<string, object>(),
            BodyKey: "nonexistent");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => store.GetAsync(item));
        Assert.Contains("No content body found", ex.Message);
    }

    [Fact]
    public async Task GetAsync_Cancellation_Throws()
    {
        var store = new DictionaryContentBodyStore(new Dictionary<string, ContentBody>());
        var item = new ContentItem(
            Id: "test", Title: "Test", Slug: "test",
            PublishAt: default, ContentHtml: null,
            Meta: new Dictionary<string, object>(),
            BodyKey: "test");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => store.GetAsync(item, cts.Token));
    }
}
