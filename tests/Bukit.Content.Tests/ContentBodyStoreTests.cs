using Xunit;

namespace Bukit.Content.Tests;

public sealed class ContentBodyStoreTests
{
    [Fact]
    public async Task EmptyContentBodyStore_WithInlineHtml_ReturnsInlineBody()
    {
        var item = Item("page-1", contentHtml: "<p>inline</p>");

        var body = await EmptyContentBodyStore.Instance.GetAsync(item);

        Assert.Equal("<p>inline</p>", body.Html);
    }

    [Fact]
    public async Task EmptyContentBodyStore_WithoutInlineHtml_Throws()
    {
        var item = Item("page-1");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            EmptyContentBodyStore.Instance.GetAsync(item));

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

        var stored = await store.GetAsync(Item("page-1", bodyKey: "body-1"));
        var inline = await store.GetAsync(Item("page-2", contentHtml: "<p>inline</p>", bodyKey: "missing"));

        Assert.Equal("<p>stored</p>", stored.Html);
        Assert.Equal("<p>inline</p>", inline.Html);
    }

    [Fact]
    public async Task DictionaryContentBodyStore_WhenBodyMissing_ThrowsWithItemId()
    {
        var store = new DictionaryContentBodyStore(new Dictionary<string, ContentBody>());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            store.GetAsync(Item("page-1", bodyKey: "missing")));

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

        var body = await store.GetAsync(item);

        Assert.Equal("<p>from markdown</p>", body.Html);
        Assert.Same(item, inner.LastItem);
    }

    [Fact]
    public async Task CompositeContentBodyStore_WithInlineHtml_DoesNotDispatch()
    {
        var inner = new RecordingBodyStore(new ContentBody("<p>stored</p>"));
        var store = new CompositeContentBodyStore(new Dictionary<string, IContentBodyStore>
        {
            ["markdown"] = inner
        });

        var body = await store.GetAsync(Item("markdown:page-1", contentHtml: "<p>inline</p>"));

        Assert.Equal("<p>inline</p>", body.Html);
        Assert.Null(inner.LastItem);
    }

    [Fact]
    public async Task CompositeContentBodyStore_WhenSourceMissing_Throws()
    {
        var store = new CompositeContentBodyStore(new Dictionary<string, IContentBodyStore>());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            store.GetAsync(Item("notion:page-1")));

        Assert.Contains("No content body store registered", ex.Message);
        Assert.Contains("notion", ex.Message);
    }

    [Fact]
    public async Task CompositeContentBodyStore_WhenIdHasNoSourcePrefix_Throws()
    {
        var store = new CompositeContentBodyStore(new Dictionary<string, IContentBodyStore>());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            store.GetAsync(Item("page-1")));

        Assert.Contains("Unable to resolve content body store", ex.Message);
        Assert.Contains("page-1", ex.Message);
    }

    private static ContentItem Item(string id, string? contentHtml = null, string? bodyKey = null)
    {
        return new ContentItem(
            Id: id,
            Title: id,
            Slug: id,
            PublishAt: DateTimeOffset.UnixEpoch,
            ContentHtml: contentHtml,
            Meta: new Dictionary<string, object>(),
            Fields: null,
            BodyKey: bodyKey);
    }

    private sealed class RecordingBodyStore : IContentBodyStore
    {
        private readonly ContentBody _body;

        public RecordingBodyStore(ContentBody body)
        {
            _body = body;
        }

        public ContentItem? LastItem { get; private set; }

        public Task<ContentBody> GetAsync(ContentItem item, CancellationToken cancellationToken = default)
        {
            LastItem = item;
            return Task.FromResult(_body);
        }
    }
}
