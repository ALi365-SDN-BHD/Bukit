using Bukit.Engine.Abstractions.Content;
using Xunit;

namespace Bukit.Content.Tests;

public sealed class CompositeContentBodyStoreTests
{
    private static ContentDocument MakeItem(string id, string? contentHtml = null, string? sourceId = null, string? bodyKey = null)
    {
        var fields = new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase);
        if (sourceId != null)
            fields["sourceId"] = new ContentField("text", sourceId);
        return ContentDocument.Create(
            id: id, title: "", slug: "",
            publishAt: default, contentHtml: contentHtml,
            fields: fields, bodyKey: bodyKey);
    }

    [Fact]
    public async Task GetAsync_HasContentHtml_ReturnsImmediately()
    {
        var inner = new DictionaryContentBodyStore(new Dictionary<string, ContentBody>());
        var store = new CompositeContentBodyStore(new Dictionary<string, IContentBodyStore> { ["md"] = inner });
        var item = MakeItem("md:test", "<p>inline</p>");

        var body = await store.GetAsync(item.ToDocument());

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

        var body = await store.GetAsync(item.ToDocument());

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

        var body = await store.GetAsync(item.ToDocument());

        Assert.Same(bodyContent, body);
    }

    [Fact]
    public async Task GetAsync_NoSeparator_Throws()
    {
        var store = new CompositeContentBodyStore(new Dictionary<string, IContentBodyStore>());
        var item = MakeItem("no-separator");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => store.GetAsync(item.ToDocument()));

        Assert.Contains("Unable to resolve", ex.Message);
    }

    [Fact]
    public async Task GetAsync_UnknownSource_Throws()
    {
        var store = new CompositeContentBodyStore(new Dictionary<string, IContentBodyStore>());
        var item = MakeItem("unknown:test");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => store.GetAsync(item.ToDocument()));

        Assert.Contains("No content body store registered", ex.Message);
    }

    private sealed class CountingDisposeStore : IContentBodyStore, IAsyncDisposable
    {
        public int DisposeCount { get; private set; }

        public Task<ContentBody> GetAsync(ContentDocument item, CancellationToken cancellationToken = default)
            => Task.FromResult(new ContentBody("<p>counting</p>"));

        public ValueTask DisposeAsync()
        {
            DisposeCount++;
            return ValueTask.CompletedTask;
        }
    }

    [Fact]
    public async Task DisposeAsync_DisposesEachDistinctChildStoreExactlyOnce()
    {
        var shared = new CountingDisposeStore();
        var other = new CountingDisposeStore();
        var store = new CompositeContentBodyStore(new Dictionary<string, IContentBodyStore>
        {
            ["a"] = shared,
            ["b"] = other,
            ["c"] = shared
        });

        await ((IAsyncDisposable)(object)store).DisposeAsync();

        Assert.Equal(1, shared.DisposeCount);
        Assert.Equal(1, other.DisposeCount);
    }

    [Fact]
    public async Task DisposeAsync_DuplicateSourceKeys_DisposesEveryOrderedStoreExactlyOnce()
    {
        var first = new CountingDisposeStore();
        var second = new CountingDisposeStore();
        var store = new CompositeContentBodyStore(
        [
            ("duplicate", first),
            ("DUPLICATE", second)
        ]);

        await store.DisposeAsync();

        Assert.Equal(1, first.DisposeCount);
        Assert.Equal(1, second.DisposeCount);
    }
}
