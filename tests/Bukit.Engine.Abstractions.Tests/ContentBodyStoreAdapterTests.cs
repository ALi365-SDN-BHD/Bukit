using Bukit.Engine.Abstractions.Content;
using Xunit;

namespace Bukit.Engine.Abstractions.Tests;

public sealed class ContentBodyStoreAdapterTests
{
    [Fact]
    public async Task GetAsync_RawDataWithoutCollection_UsesModuleTypeAndEmptyCollection()
    {
        var fields = new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
        {
            ["sourceMode"] = new("text", "data")
        };
        var raw = new RawContentDocument(
            Id: "site-data",
            Title: "Site data",
            Slug: "site-data",
            PublishAt: DateTimeOffset.UnixEpoch,
            Body: new RawBody(),
            Properties: RawContentValue.FromFields(fields),
            CustomFields: fields);
        var store = new RecordingBodyStore();

        await ((IContentBodyStore)store).GetAsync(raw);

        Assert.NotNull(store.Document);
        Assert.Equal("module", store.Document.Record.Identity.ContentType);
        Assert.Equal(string.Empty, store.Document.Record.Classification.Collection);
    }

    private sealed class RecordingBodyStore : IContentBodyStore
    {
        public ContentDocument? Document { get; private set; }

        public Task<ContentBody> GetAsync(ContentDocument document, CancellationToken cancellationToken = default)
        {
            Document = document;
            return Task.FromResult(new ContentBody(string.Empty));
        }
    }
}
