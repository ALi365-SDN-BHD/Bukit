using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Routing;
using Xunit;

namespace Bukit.Routing.Tests;

public sealed class RouteGeneratorTests
{
    [Fact]
    public void Generate_WithCollectionPermalink()
    {
        var record = new ContentRecord(
            new ContentIdentity("p2", "my-post", "my-post", "post", "published"),
            new ContentPresentation("My Post", null, null, "en", Array.Empty<string>()),
            new ContentClassification("post", "posts", Array.Empty<string>(), Array.Empty<string>()),
            new ContentOwnership(null, null, null, null),
            new ContentLifecycle(DateTimeOffset.UtcNow, null, null, null),
            new ProvenanceRecord(null, null, Array.Empty<string>(), Array.Empty<string>(), null),
            new TrustMetadata(null, "unchecked", Array.Empty<string>()),
            Array.Empty<EntityRecord>(), Array.Empty<ContentRelation>(), Array.Empty<MediaAsset>());
        var doc = new ContentDocument(record, new ContentBodyRef(""));
        var collections = new Dictionary<string, RouteGenerator.CollectionRouteRule>
        {
            ["posts"] = new("/blog/{slug}/", "pages/post.html")
        };
        var route = RouteGenerator.Generate(doc, collections: collections);
        Assert.Contains("/blog/", route.Url);
    }
}
