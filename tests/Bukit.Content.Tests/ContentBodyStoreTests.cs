using Bukit.Engine.Abstractions.Content;
using Xunit;

namespace Bukit.Content.Tests;

public sealed class ContentBodyStoreTests
{
    [Fact]
    public async Task EmptyContentBodyStore_WithInlineHtml_ReturnsInlineBody()
    {
        var item = Item("page-1", contentHtml: "<p>inline</p>");

        var body = await EmptyContentBodyStore.Instance.GetAsync(item.ToDocument());

        Assert.Equal("<p>inline</p>", body.Html);
    }

    [Fact]
    public async Task EmptyContentBodyStore_WithoutInlineHtml_Throws()
    {
        var item = Item("page-1");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            EmptyContentBodyStore.Instance.GetAsync(item.ToDocument()));

        Assert.Contains("No content body available", ex.Message);
        Assert.Contains("page-1", ex.Message);
    }

    [Fact]
    public async Task DictionaryContentBodyStore_UsesBodyKeyAndHonorsInlineHtml()
    {
        var store = new DictionaryContentBodyStore(new Dictionary<string, ContentBody>(StringComparer.OrdinalIgnoreCase)
        {
            ["body-1"] = new("<p>stored</p>")
        });

        var stored = await store.GetAsync(Item("page-1", bodyKey: "body-1").ToDocument());
        var inline = await store.GetAsync(Item("page-2", contentHtml: "<p>inline</p>", bodyKey: "missing").ToDocument());

        Assert.Equal("<p>stored</p>", stored.Html);
        Assert.Equal("<p>inline</p>", inline.Html);
    }

    [Fact]
    public async Task DictionaryContentBodyStore_WhenBodyMissing_ThrowsWithItemId()
    {
        var store = new DictionaryContentBodyStore(new Dictionary<string, ContentBody>());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            store.GetAsync(Item("page-1", bodyKey: "missing").ToDocument()));

        Assert.Contains("No content body found", ex.Message);
        Assert.Contains("page-1", ex.Message);
    }

    [Fact]
    public async Task CompositeContentBodyStore_DispatchesBySourcePrefix()
    {
        var inner = new RecordingBodyStore(new ContentBody("<p>from markdown</p>"));
        var store = new CompositeContentBodyStore(new Dictionary<string, IContentBodyStore>(StringComparer.OrdinalIgnoreCase)
        {
            ["markdown"] = inner
        });
        var item = Item("markdown:page-1");

        var body = await store.GetAsync(item.ToDocument());

        Assert.Equal("<p>from markdown</p>", body.Html);
        Assert.Equal(item.Id, inner.LastDocument?.Id);
    }

    [Fact]
    public async Task CompositeContentBodyStore_WithInlineHtml_DoesNotDispatch()
    {
        var inner = new RecordingBodyStore(new ContentBody("<p>stored</p>"));
        var store = new CompositeContentBodyStore(new Dictionary<string, IContentBodyStore>
        {
            ["markdown"] = inner
        });

        var body = await store.GetAsync(Item("markdown:page-1", contentHtml: "<p>inline</p>").ToDocument());

        Assert.Equal("<p>inline</p>", body.Html);
        Assert.Null(inner.LastDocument);
    }

    [Fact]
    public async Task CompositeContentBodyStore_WhenSourceMissing_Throws()
    {
        var store = new CompositeContentBodyStore(new Dictionary<string, IContentBodyStore>());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            store.GetAsync(Item("notion:page-1").ToDocument()));

        Assert.Contains("No content body store registered", ex.Message);
        Assert.Contains("notion", ex.Message);
    }

    [Fact]
    public async Task CompositeContentBodyStore_WhenIdHasNoSourcePrefix_Throws()
    {
        var store = new CompositeContentBodyStore(new Dictionary<string, IContentBodyStore>());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            store.GetAsync(Item("page-1").ToDocument()));

        Assert.Contains("Unable to resolve content body store", ex.Message);
        Assert.Contains("page-1", ex.Message);
    }

    private static ContentDocument Item(string id, string? contentHtml = null, string? bodyKey = null)
    {
        return ContentDocument.Create(
            id: id,
            title: id,
            slug: id,
            publishAt: DateTimeOffset.UnixEpoch,
            contentHtml: contentHtml,
            fields: null,
            bodyKey: bodyKey);
    }

    private sealed class RecordingBodyStore : IContentBodyStore
    {
        private readonly ContentBody _body;

        public RecordingBodyStore(ContentBody body)
        {
            _body = body;
        }

        public ContentDocument? LastDocument { get; private set; }

        public Task<ContentBody> GetAsync(ContentDocument item, CancellationToken cancellationToken = default)
        {
            LastDocument = item;
            return Task.FromResult(_body);
        }
    }
}
