using Bukit.Cli.Commands;
using Bukit.Config;
using Bukit.Engine.Abstractions.Content;
using Xunit;

namespace Bukit.Cli.Tests;

public sealed class RouteCommandTests
{
    [Fact]
    public void BuildInspectEntries_UsesContentDocumentsAndSkipsDataModules()
    {
        var content = Document("post-1", "hello", isDataModule: false);
        var data = Document("data-1", "hero", isDataModule: true);
        var config = new SiteConfig
        {
            Name = "test",
            Title = "Test",
            Collections = new Dictionary<string, CollectionConfig>(StringComparer.OrdinalIgnoreCase)
            {
                ["post"] = new CollectionConfig
                {
                    Permalink = "/blog/{slug}/",
                    Template = "pages/post.html"
                }
            }
        };

        var entries = RouteCommand.BuildInspectEntries(new[] { content, data }, config);

        var entry = Assert.Single(entries);
        Assert.Equal("/blog/hello/", entry.Url);
        Assert.Equal("post", entry.Collection);
        Assert.Equal("post", entry.Type);
        Assert.Equal("en", entry.Language);
    }

    private static ContentDocument Document(string id, string slug, bool isDataModule)
    {
        var type = isDataModule ? "hero" : "post";
        var record = new ContentRecord(
            Identity: new ContentIdentity(id, slug, id, type, "published"),
            Presentation: new ContentPresentation(id, null, null, "en", Array.Empty<string>()),
            Classification: new ContentClassification(type, type, Array.Empty<string>(), Array.Empty<string>()),
            Ownership: new ContentOwnership(null, null, null, null),
            Lifecycle: new ContentLifecycle(DateTimeOffset.UtcNow, null, null, null),
            Provenance: new ProvenanceRecord(null, null, Array.Empty<string>(), Array.Empty<string>(), null),
            Trust: new TrustMetadata(null, "approved", Array.Empty<string>()),
            Entities: Array.Empty<EntityRecord>(),
            Relations: Array.Empty<ContentRelation>(),
            Media: Array.Empty<MediaAsset>());

        return new ContentDocument(
            record,
            new ContentBodyRef(null, null, null, null),
            new ContentRoutePolicy(null, null, null, null, type),
            new ContentPublishPolicy(false, false, false, false, false, false, isDataModule),
            new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase),
            Array.Empty<ContentDiagnostic>());
    }
}
