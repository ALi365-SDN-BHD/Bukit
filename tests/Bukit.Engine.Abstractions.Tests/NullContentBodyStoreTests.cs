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
        var document = Document(
            "test-id",
            "Test Title",
            "test-slug",
            DateTimeOffset.UtcNow,
            "<p>hello</p>",
            null);

        var body = await store.GetAsync(document);

        Assert.NotNull(body);
        Assert.Equal("<p>hello</p>", body.Html);
    }

    [Fact]
    public async Task GetAsync_WithoutContent_Throws()
    {
        var store = NullContentBodyStore.Instance;
        var document = Document(
            "test-id",
            "Test Title",
            "test-slug",
            DateTimeOffset.UtcNow,
            null,
            null);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => store.GetAsync(document));

        Assert.Contains("test-id", ex.Message);
    }

    [Fact]
    public async Task GetAsync_Cancelled_Throws()
    {
        var store = NullContentBodyStore.Instance;
        var document = Document(
            "test-id",
            "Test Title",
            "test-slug",
            DateTimeOffset.UtcNow,
            "<p>hello</p>",
            null);

        var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => store.GetAsync(document, cts.Token));
    }

    private static ContentDocument Document(
        string id,
        string title,
        string slug,
        DateTimeOffset publishAt,
        string? contentHtml,
        IReadOnlyDictionary<string, ContentField>? fields)
    {
        var record = new ContentRecord(
            new ContentIdentity(id, slug, slug, "page", "published"),
            new ContentPresentation(title, null, contentHtml, "und", Array.Empty<string>()),
            new ContentClassification("page", "page", Array.Empty<string>(), Array.Empty<string>()),
            new ContentOwnership(null, null, null, null),
            new ContentLifecycle(publishAt, null, null, null),
            new ProvenanceRecord(null, null, Array.Empty<string>(), Array.Empty<string>(), null),
            new TrustMetadata(null, "published", Array.Empty<string>()),
            Array.Empty<EntityRecord>(),
            Array.Empty<ContentRelation>(),
            Array.Empty<MediaAsset>());

        return new ContentDocument(record, contentHtml, fields, null);
    }
}
