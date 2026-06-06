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
        var item = ContentDocument.Create(
            id: "post-1", title: "Post", slug: "post",
            publishAt: default, contentHtml: null,
            fields: ContentFieldReader.ToFieldMap(new Dictionary<string, object>()),
            bodyKey: "post-1");

        var result = await store.GetAsync(item.ToDocument());

        Assert.Same(body, result);
    }

    [Fact]
    public async Task GetAsync_HasContentHtml_ReturnsInline()
    {
        var store = new DictionaryContentBodyStore(new Dictionary<string, ContentBody>());
        var item = ContentDocument.Create(
            id: "inline", title: "Inline", slug: "inline",
            publishAt: default, contentHtml: "<p>inline</p>",
            fields: ContentFieldReader.ToFieldMap(new Dictionary<string, object>()));

        var body = await store.GetAsync(item.ToDocument());

        Assert.Equal("<p>inline</p>", body.Html);
    }

    [Fact]
    public async Task GetAsync_EmptyBodyKey_Throws()
    {
        var store = new DictionaryContentBodyStore(new Dictionary<string, ContentBody>());
        var item = ContentDocument.Create(
            id: "missing", title: "Missing", slug: "missing",
            publishAt: default, contentHtml: null,
            fields: ContentFieldReader.ToFieldMap(new Dictionary<string, object>()),
            bodyKey: "");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => store.GetAsync(item.ToDocument()));
        Assert.Contains("No content body found", ex.Message);
    }

    [Fact]
    public async Task GetAsync_NullBodyKey_Throws()
    {
        var store = new DictionaryContentBodyStore(new Dictionary<string, ContentBody>());
        var item = ContentDocument.Create(
            id: "missing", title: "Missing", slug: "missing",
            publishAt: default, contentHtml: null,
            fields: ContentFieldReader.ToFieldMap(new Dictionary<string, object>()),
            bodyKey: null);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => store.GetAsync(item.ToDocument()));
        Assert.Contains("No content body found", ex.Message);
    }

    [Fact]
    public async Task GetAsync_MissingBodyKey_Throws()
    {
        var store = new DictionaryContentBodyStore(new Dictionary<string, ContentBody>());
        var item = ContentDocument.Create(
            id: "missing", title: "Missing", slug: "missing",
            publishAt: default, contentHtml: null,
            fields: ContentFieldReader.ToFieldMap(new Dictionary<string, object>()),
            bodyKey: "nonexistent");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => store.GetAsync(item.ToDocument()));
        Assert.Contains("No content body found", ex.Message);
    }

    [Fact]
    public async Task GetAsync_Cancellation_Throws()
    {
        var store = new DictionaryContentBodyStore(new Dictionary<string, ContentBody>());
        var item = ContentDocument.Create(
            id: "test", title: "Test", slug: "test",
            publishAt: default, contentHtml: null,
            fields: ContentFieldReader.ToFieldMap(new Dictionary<string, object>()),
            bodyKey: "test");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => store.GetAsync(item.ToDocument(), cts.Token));
    }
}
