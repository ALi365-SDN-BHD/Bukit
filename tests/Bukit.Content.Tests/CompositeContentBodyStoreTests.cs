using Bukit.Engine.Abstractions.Content;
using Xunit;

namespace Bukit.Content.Tests;

public sealed class CompositeContentBodyStoreTests
{
    private static ContentItem MakeItem(string id, string? contentHtml = null, string? sourceId = null, string? bodyKey = null)
    {
        var fields = new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase);
        if (sourceId != null)
            fields["sourceId"] = new ContentField("text", sourceId);
        return new ContentItem(
            Id: id, Title: "", Slug: "",
            PublishAt: default, ContentHtml: contentHtml,
            Fields: fields, BodyKey: bodyKey);
    }

    [Fact]
    public async Task GetAsync_HasContentHtml_ReturnsImmediately()
    {
        var inner = new DictionaryContentBodyStore(new Dictionary<string, ContentBody>());
        var store = new CompositeContentBodyStore(new Dictionary<string, IContentBodyStore> { ["md"] = inner });
        var item = MakeItem("md:test", "<p>inline</p>");

        var body = await store.GetAsync(item);

        Assert.Equal("<p>inline</p>", body.Html);
    }

    [Fact]
    public async Task GetAsync_WithSourceId_UsesSourceIdAsId()
    {
        var bodyContent = new ContentBody("<p>from source</p>");
        var inner = new DictionaryContentBodyStore(new Dictionary<string, ContentBody>
        {
            ["actual-id"] = bodyContent
        });
        var store = new CompositeContentBodyStore(new Dictionary<string, IContentBodyStore>
        {
            ["md"] = inner,
            ["notion"] = new DictionaryContentBodyStore(new Dictionary<string, ContentBody>())
        });
        var item = MakeItem("md:123", sourceId: "actual-id", bodyKey: "actual-id");

        var body = await store.GetAsync(item);

        Assert.Same(bodyContent, body);
    }

    [Fact]
    public async Task GetAsync_NoSourceId_UsesOriginalId()
    {
        var bodyContent = new ContentBody("<p>direct</p>");
        var inner = new DictionaryContentBodyStore(new Dictionary<string, ContentBody>
        {
            ["my-page"] = bodyContent
        });
        var store = new CompositeContentBodyStore(new Dictionary<string, IContentBodyStore>
        {
            ["md"] = inner
        });
        var item = MakeItem("md:my-page", bodyKey: "my-page");

        var body = await store.GetAsync(item);

        Assert.Same(bodyContent, body);
    }

    [Fact]
    public async Task GetAsync_NoSeparator_Throws()
    {
        var store = new CompositeContentBodyStore(new Dictionary<string, IContentBodyStore>());
        var item = MakeItem("no-separator");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => store.GetAsync(item));

        Assert.Contains("Unable to resolve", ex.Message);
    }

    [Fact]
    public async Task GetAsync_UnknownSource_Throws()
    {
        var store = new CompositeContentBodyStore(new Dictionary<string, IContentBodyStore>());
        var item = MakeItem("unknown:test");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => store.GetAsync(item));

        Assert.Contains("No content body store registered", ex.Message);
    }
}
