using Bukit.Engine.Abstractions.Content;
using System.Collections.ObjectModel;
using Xunit;

namespace Bukit.Engine.Abstractions.Tests;

public sealed class ContentBodyStoreAdapterTests
{
    [Fact]
    public async Task GetAsync_RawPropertiesOnlyContent_PreservesTypeAndCollection()
    {
        var fields = new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
        {
            ["type"] = new("text", "article"),
            ["collection"] = new("text", "news")
        };
        var raw = new RawContentDocument(
            Id: "news-item",
            Title: "News item",
            Slug: "news-item",
            PublishAt: DateTimeOffset.UnixEpoch,
            Body: new RawBody(),
            Properties: RawContentValue.FromFields(fields));
        var store = new RecordingBodyStore();

        await ((IContentBodyStore)store).GetAsync(raw);

        Assert.NotNull(store.Document);
        Assert.Equal("article", store.Document.Record.Identity.ContentType);
        Assert.Equal("news", store.Document.Record.Classification.Collection);
    }

    [Fact]
    public async Task GetAsync_RawPropertiesOnlyDataWithoutCollection_UsesModuleTypeAndEmptyCollection()
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
            Properties: RawContentValue.FromFields(fields));
        var store = new RecordingBodyStore();

        await ((IContentBodyStore)store).GetAsync(raw);

        Assert.NotNull(store.Document);
        Assert.Equal("module", store.Document.Record.Identity.ContentType);
        Assert.Equal(string.Empty, store.Document.Record.Classification.Collection);
    }

    [Fact]
    public async Task GetAsync_RawCustomFieldsOverridePropertiesAcrossCanonicalProjections()
    {
        var properties = new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
        {
            ["type"] = new("text", "page"),
            ["collection"] = new("text", "pages"),
            ["url"] = new("text", "/from-properties/"),
            ["draft"] = new("bool", false),
            ["summary"] = new("text", "Property summary")
        };
        var customFields = new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
        {
            ["type"] = new("text", "article"),
            ["collection"] = new("text", "news"),
            ["url"] = new("text", "/from-custom-fields/"),
            ["draft"] = new("bool", true)
        };
        var raw = new RawContentDocument(
            Id: "news-item",
            Title: "News item",
            Slug: "news-item",
            PublishAt: DateTimeOffset.UnixEpoch,
            Body: new RawBody(),
            Properties: RawContentValue.FromFields(properties),
            CustomFields: customFields);
        var store = new RecordingBodyStore();

        await ((IContentBodyStore)store).GetAsync(raw);

        Assert.NotNull(store.Document);
        Assert.Equal("article", store.Document.Record.Identity.ContentType);
        Assert.Equal("news", store.Document.Record.Classification.Collection);
        Assert.Equal("Property summary", store.Document.Record.Presentation.Summary);
        Assert.Equal("/from-custom-fields/", store.Document.Route.Url);
        Assert.True(store.Document.Publish.Draft);
        Assert.Equal("article", ContentFieldReader.GetText(store.Document.CustomFields, "type"));
        Assert.Equal("Property summary", ContentFieldReader.GetText(store.Document.CustomFields, "summary"));
    }

    [Fact]
    public async Task GetAsync_RawMutableCustomFields_PreservesReferenceAndAddsMissingProperties()
    {
        var properties = new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
        {
            ["type"] = new("text", "article"),
            ["collection"] = new("text", "news"),
            ["summary"] = new("text", "Property summary")
        };
        var customFields = new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
        {
            ["collection"] = new("text", "briefings")
        };
        var raw = new RawContentDocument(
            Id: "briefing",
            Title: "Briefing",
            Slug: "briefing",
            PublishAt: DateTimeOffset.UnixEpoch,
            Body: new RawBody(),
            Properties: RawContentValue.FromFields(properties),
            CustomFields: customFields);
        var store = new RecordingBodyStore();

        await ((IContentBodyStore)store).GetAsync(raw);

        Assert.NotNull(store.Document);
        Assert.Same(customFields, store.Document.CustomFields);
        Assert.Equal("article", ContentFieldReader.GetText(customFields, "type"));
        Assert.Equal("briefings", ContentFieldReader.GetText(customFields, "collection"));
        Assert.Equal("Property summary", ContentFieldReader.GetText(customFields, "summary"));
    }

    [Fact]
    public async Task GetAsync_RawReadOnlyCustomFields_ClonesAndMergesWithoutMutation()
    {
        var properties = new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
        {
            ["type"] = new("text", "article"),
            ["collection"] = new("text", "news")
        };
        var sourceFields = new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
        {
            ["collection"] = new("text", "briefings")
        };
        var customFields = new ReadOnlyDictionary<string, ContentField>(sourceFields);
        var raw = new RawContentDocument(
            Id: "briefing",
            Title: "Briefing",
            Slug: "briefing",
            PublishAt: DateTimeOffset.UnixEpoch,
            Body: new RawBody(),
            Properties: RawContentValue.FromFields(properties),
            CustomFields: customFields);
        var store = new RecordingBodyStore();

        await ((IContentBodyStore)store).GetAsync(raw);

        Assert.NotNull(store.Document);
        Assert.NotSame(customFields, store.Document.CustomFields);
        Assert.Equal("article", ContentFieldReader.GetText(store.Document.CustomFields, "type"));
        Assert.Equal("briefings", ContentFieldReader.GetText(store.Document.CustomFields, "collection"));
        Assert.False(sourceFields.ContainsKey("type"));
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
