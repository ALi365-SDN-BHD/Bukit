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
        var doc = new ContentDocument(
            record,
            new ContentBodyRef(""),
            customFields: ContentFieldReader.ToFieldMap(new Dictionary<string, object>
            {
                ["collection"] = "posts"
            }));
        var collections = new Dictionary<string, RouteGenerator.CollectionRouteRule>
        {
            ["posts"] = new("/blog/{slug}/", "pages/post.html")
        };
        var route = RouteGenerator.Generate(doc, collections: collections);
        Assert.Contains("/blog/", route.Url);
    }

    [Fact]
    public void GenerateWithSource_FullOverride_ReturnsFullOverride()
    {
        var document = CreateDocument(
            "full",
            new Dictionary<string, object>
            {
                ["collection"] = "posts",
                ["route"] = new Dictionary<string, object>
                {
                    ["url"] = "/full/",
                    ["template"] = "pages/full.html"
                }
            });

        var result = RouteGenerator.GenerateWithSource(document);

        Assert.Equal(RouteGenerator.RouteSource.FullOverride, result.Source);
        Assert.Equal(
            new RouteInfo("/full/", "full/index.html", "pages/full.html"),
            result.Route);
    }

    [Fact]
    public void GenerateWithSource_PartialOverride_ReturnsPartialOverride()
    {
        var document = CreateDocument(
            "partial",
            new Dictionary<string, object>
            {
                ["collection"] = "posts",
                ["route"] = new Dictionary<string, object>
                {
                    ["url"] = "/partial/"
                }
            });
        var collections = new Dictionary<string, RouteGenerator.CollectionRouteRule>
        {
            ["posts"] = new("/blog/{slug}/", "pages/post.html")
        };

        var result = RouteGenerator.GenerateWithSource(
            document,
            collections: collections);

        Assert.Equal(
            RouteGenerator.RouteSource.PartialOverride,
            result.Source);
        Assert.Equal(
            new RouteInfo(
                "/partial/",
                "partial/index.html",
                "pages/post.html"),
            result.Route);
    }

    [Fact]
    public void GenerateWithSource_CollectionRule_ReturnsNamedTuple()
    {
        var document = CreateDocument(
            "collection",
            new Dictionary<string, object>
            {
                ["collection"] = "posts"
            });
        var collections = new Dictionary<string, RouteGenerator.CollectionRouteRule>
        {
            ["posts"] = new("/blog/{slug}/", "pages/post.html")
        };

        var result = RouteGenerator.GenerateWithSource(
            document,
            collections: collections);
        (RouteInfo route, RouteGenerator.RouteSource source) = result;

        Assert.Equal(RouteGenerator.RouteSource.Collection, result.Source);
        Assert.Equal(result.Route, route);
        Assert.Equal(result.Source, source);
        Assert.Equal("/blog/collection/", route.Url);
    }

    [Fact]
    public void GenerateWithSource_TypePermalink_ReturnsPermalink()
    {
        var document = CreateDocument(
            "permalink",
            new Dictionary<string, object>
            {
                ["type"] = "article",
                ["collection"] = "news"
            });
        var permalinks = new Dictionary<string, string>
        {
            ["article"] = "/articles/{slug}/"
        };

        var result = RouteGenerator.GenerateWithSource(
            document,
            permalinks: permalinks);

        Assert.Equal(RouteGenerator.RouteSource.Permalink, result.Source);
        Assert.Equal("/articles/permalink/", result.Route.Url);
    }

    private static ContentDocument CreateDocument(
        string slug,
        IReadOnlyDictionary<string, object> fieldValues)
    {
        var record = new ContentRecord(
            new ContentIdentity(
                slug,
                slug,
                slug,
                "post",
                "published"),
            new ContentPresentation(
                slug,
                null,
                null,
                "en",
                Array.Empty<string>()),
            new ContentClassification(
                "post",
                "posts",
                Array.Empty<string>(),
                Array.Empty<string>()),
            new ContentOwnership(null, null, null, null),
            new ContentLifecycle(
                DateTimeOffset.UnixEpoch,
                null,
                null,
                null),
            new ProvenanceRecord(
                null,
                null,
                Array.Empty<string>(),
                Array.Empty<string>(),
                null),
            new TrustMetadata(
                null,
                "unchecked",
                Array.Empty<string>()),
            Array.Empty<EntityRecord>(),
            Array.Empty<ContentRelation>(),
            Array.Empty<MediaAsset>());

        return new ContentDocument(
            record,
            new ContentBodyRef(""),
            customFields: ContentFieldReader.ToFieldMap(fieldValues));
    }
}
