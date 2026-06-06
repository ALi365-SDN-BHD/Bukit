using Bukit.Engine.Abstractions.Content;
using Xunit;

namespace Bukit.Engine.Abstractions.Tests;

public sealed class ContentDocumentTests
{
    [Fact]
    public void Constructor_ShouldExposeTypedRuntimeState_WhenNormalizedContentIsProvided()
    {
        var record = new ContentRecord(
            Identity: new ContentIdentity("post-1", "hello", "post-1", "post", "published"),
            Presentation: new ContentPresentation("Hello", "Summary", "<p>Hello</p>", "en", Array.Empty<string>()),
            Classification: new ContentClassification("post", "posts", ["updates"], ["ai"]),
            Ownership: new ContentOwnership("Ada", "Bukit", "owner", "reviewer"),
            Lifecycle: new ContentLifecycle(
                new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 6, 2, 0, 0, 0, TimeSpan.Zero),
                null,
                null),
            Provenance: new ProvenanceRecord("markdown", "https://example.test/original", ["citation"], ["reference"], "synced"),
            Trust: new TrustMetadata(0.9, "reviewed", ["original"]),
            Entities: [new EntityRecord("company", "Bukit", "CMS infrastructure")],
            Relations: [new ContentRelation("mentions", "company:Bukit")],
            Media: [new MediaAsset("image", "/img/cover.png", "Cover")]);
        var document = new ContentDocument(
            Record: record,
            Body: new ContentBodyRef(Html: "<p>Hello</p>", BodyKey: "body-1", Markdown: "# Hello", PlainText: "Hello"),
            Route: new ContentRoutePolicy(Url: "/posts/hello/", OutputPath: "posts/hello/index.html", Template: "pages/post.html", PermalinkPattern: "/posts/{slug}/", ListGroup: "posts"),
            Publish: new ContentPublishPolicy(Draft: false, NoIndex: false, NoFollow: false, ExcludeFromFeed: false, ExcludeFromSearch: false, ExcludeFromSitemap: false, IsDataModule: false),
            CustomFields: new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
            {
                ["featured"] = new("bool", true)
            },
            Diagnostics: [new ContentDiagnostic("content.ok", "info", "Normalized", null, "post-1")]);

        Assert.Equal("post-1", document.Record.Identity.Id);
        Assert.Equal("/posts/hello/", document.Route.Url);
        Assert.False(document.Publish.Draft);
        Assert.True((bool)document.CustomFields["featured"].Value!);
        Assert.Single(document.Diagnostics);
    }

    [Fact]
    public void CanonicalContentGraph_ShouldExposeDocumentsAndRelations_WhenVNextDocumentsAreProvided()
    {
        var document = CreateDocument("post-1");
        var relation = new ContentRelation("translation-of", "post-0");
        var graph = new CanonicalContentGraph(
            Records: [document.Record],
            Entities: document.Record.Entities,
            Documents: [document],
            Relations: [relation]);

        Assert.Single(graph.Documents);
        Assert.Equal("post-1", graph.Documents[0].Record.Identity.Id);
        Assert.Single(graph.Relations);
        Assert.Equal("translation-of", graph.Relations[0].Type);
    }

    private static ContentDocument CreateDocument(string id)
    {
        var record = new ContentRecord(
            Identity: new ContentIdentity(id, "hello", id, "post", "published"),
            Presentation: new ContentPresentation("Hello", "Summary", "<p>Hello</p>", "en", Array.Empty<string>()),
            Classification: new ContentClassification("post", "posts", Array.Empty<string>(), Array.Empty<string>()),
            Ownership: new ContentOwnership(null, null, null, null),
            Lifecycle: new ContentLifecycle(DateTimeOffset.UtcNow, null, null, null),
            Provenance: new ProvenanceRecord("markdown", null, Array.Empty<string>(), Array.Empty<string>(), "synced"),
            Trust: new TrustMetadata(null, "draft", Array.Empty<string>()),
            Entities: Array.Empty<EntityRecord>(),
            Relations: Array.Empty<ContentRelation>(),
            Media: Array.Empty<MediaAsset>());

        return new ContentDocument(
            record,
            new ContentBodyRef("<p>Hello</p>", "body-1", "# Hello", "Hello"),
            new ContentRoutePolicy("/posts/hello/", "posts/hello/index.html", "pages/post.html", null, "posts"),
            new ContentPublishPolicy(false, false, false, false, false, false, false),
            new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase),
            Array.Empty<ContentDiagnostic>());
    }
}
