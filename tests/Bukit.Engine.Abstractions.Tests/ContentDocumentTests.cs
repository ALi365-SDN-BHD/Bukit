using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Routing;
using Xunit;

namespace Bukit.Engine.Abstractions.Tests;

public sealed class ContentDocumentTests
{
    private static ContentDocument CreateDoc(
        string? status = null,
        bool draft = false,
        string collection = "post",
        IReadOnlyDictionary<string, ContentField>? extraFields = null)
    {
        var fields = new Dictionary<string, ContentField>();
        if (extraFields != null)
        {
            foreach (var kv in extraFields) fields[kv.Key] = kv.Value;
        }
        if (draft)
        {
            fields["draft"] = new ContentField("bool", true);
        }
        if (status != null)
        {
            fields["status"] = new ContentField("text", status);
        }
        return ContentDocument.Create("post-1", "Test Post", "test-post",
            new DateTimeOffset(2026, 6, 9, 0, 0, 0, TimeSpan.Zero),
            "<p>Hello</p>", fields, "markdown");
    }

    [Fact]
    public void Create_DefaultsToPublishedAndPageType()
    {
        var doc = CreateDoc();
        Assert.Equal("published", doc.Record.Identity.Status);
        // collection field is not set in test fields, so type defaults to "page"
        Assert.Equal("page", doc.Record.Identity.ContentType);
        Assert.Equal("test-post", doc.Record.Identity.Slug);
        Assert.Equal("Test Post", doc.Record.Presentation.Title);
    }

    [Fact]
    public void Create_DraftField_SetsStatusToDraft()
    {
        var doc = CreateDoc(draft: true);
        Assert.Equal("draft", doc.Record.Identity.Status);
    }

    [Fact]
    public void Create_ExplicitStatus_OverridesDraft()
    {
        var fields = new Dictionary<string, ContentField>
        {
            ["status"] = new ContentField("text", "review")
        };
        var doc = CreateDoc(extraFields: fields);
        Assert.Equal("review", doc.Record.Identity.Status);
    }

    [Fact]
    public void Create_DataMode_DefaultsToModuleType()
    {
        var fields = new Dictionary<string, ContentField>
        {
            ["sourceMode"] = new ContentField("text", "data")
        };
        var doc = ContentDocument.Create("mod-1", "Module", "module",
            new DateTimeOffset(2026, 6, 9, 0, 0, 0, TimeSpan.Zero),
            null, fields);
        Assert.Equal("module", doc.Record.Identity.ContentType);
        Assert.Equal(string.Empty, doc.Record.Classification.Collection);
    }

    [Fact]
    public void Create_CollectionOnly_DefaultsTypeToPageWithoutChangingCollection()
    {
        var fields = new Dictionary<string, ContentField>
        {
            ["collection"] = new ContentField("text", "news")
        };

        var doc = CreateDoc(extraFields: fields);

        Assert.Equal("page", doc.Record.Identity.ContentType);
        Assert.Equal("news", doc.Record.Classification.Collection);
    }

    [Fact]
    public void Create_TypeField_OverridesCollectionDefault()
    {
        var fields = new Dictionary<string, ContentField>
        {
            ["type"] = new ContentField("text", "landing")
        };
        var doc = CreateDoc(extraFields: fields);
        Assert.Equal("landing", doc.Record.Identity.ContentType);
    }

    [Fact]
    public void Create_SetsClassificationFromSectionsAndTags()
    {
        var fields = new Dictionary<string, ContentField>
        {
            ["sections"] = new ContentField("list", new List<object> { new ContentField("text", "hero"), new ContentField("text", "footer") }),
            ["tags"] = new ContentField("list", new List<object> { new ContentField("text", "dotnet"), new ContentField("text", "ssg") })
        };
        var doc = CreateDoc(extraFields: fields);
        Assert.Equal(2, doc.Record.Classification.Sections.Count);
        Assert.Equal(2, doc.Record.Classification.Tags.Count);
        Assert.Equal(2, doc.Record.Classification.Sections.Count);
        Assert.Equal(2, doc.Record.Classification.Tags.Count);
    }

    [Fact]
    public void ComputedProperties_MatchRecordValues()
    {
        var doc = CreateDoc();
        Assert.Equal("post-1", doc.Id);
        Assert.Equal("Test Post", doc.Title);
        Assert.Equal("test-post", doc.Slug);
        Assert.Equal(2026, doc.PublishAt.Year);
    }

    [Fact]
    public void Constructor_DefaultsRouteAndPublishAndSource()
    {
        var record = new ContentRecord(
            new ContentIdentity("x", "x", "x", "page", "published"),
            new ContentPresentation("X", null, null, "en", Array.Empty<string>()),
            new ContentClassification("page", "pages", Array.Empty<string>(), Array.Empty<string>()),
            new ContentOwnership(null, null, null, null),
            new ContentLifecycle(DateTimeOffset.UtcNow, null, null, null),
            new ProvenanceRecord(null, null, Array.Empty<string>(), Array.Empty<string>(), null),
            new TrustMetadata(null, "unchecked", Array.Empty<string>()),
            Array.Empty<EntityRecord>(),
            Array.Empty<ContentRelation>(),
            Array.Empty<MediaAsset>());
        var doc = new ContentDocument(record, new ContentBodyRef(""));
        Assert.NotNull(doc.Route);
        Assert.NotNull(doc.Publish);
        Assert.NotNull(doc.Source);
        Assert.Empty(doc.Diagnostics);
    }
}
