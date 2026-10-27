using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Routing;
using Bukit.Shared;
using Xunit;

namespace Bukit.Routing.Tests;

[Trait("Category", "Unit")]
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
    public void Generate_WithFileExtensionPermalink_PreservesFileRoute()
    {
        var document = CreateDocument(
            "file-route",
            new Dictionary<string, object> { ["collection"] = "posts" });
        var collections = new Dictionary<string, RouteGenerator.CollectionRouteRule>
        {
            ["posts"] = new("/blog/{slug}.html", "pages/post.html")
        };

        var route = RouteGenerator.Generate(document, collections: collections);

        Assert.Equal("/blog/file-route.html", route.Url);
        Assert.Equal("blog/file-route.html", route.OutputPath);
    }

    [Fact]
    public void Generate_WithDirectoryPermalinkAndDottedSlug_RemainsDirectory()
    {
        var document = CreateDocument(
            "release.2026",
            new Dictionary<string, object> { ["collection"] = "posts" });
        var collections = new Dictionary<string, RouteGenerator.CollectionRouteRule>
        {
            ["posts"] = new("/blog/{slug}", "pages/post.html")
        };

        var route = RouteGenerator.Generate(document, collections: collections);

        Assert.Equal("/blog/release.2026/", route.Url);
        Assert.Equal("blog/release.2026/index.html", route.OutputPath);
    }

    [Fact]
    public void Generate_WithFilePermalinkAndDottedSlug_PreservesFile()
    {
        var document = CreateDocument(
            "release.2026",
            new Dictionary<string, object> { ["collection"] = "posts" });
        var collections = new Dictionary<string, RouteGenerator.CollectionRouteRule>
        {
            ["posts"] = new("/blog/{slug}.html", "pages/post.html")
        };

        var route = RouteGenerator.Generate(document, collections: collections);

        Assert.Equal("/blog/release.2026.html", route.Url);
        Assert.Equal("blog/release.2026.html", route.OutputPath);
    }

    [Theory]
    [InlineData("/docs/v1.0", "/docs/v1.0/", "docs/v1.0/index.html")]
    [InlineData("/docs/v1.0/", "/docs/v1.0/", "docs/v1.0/index.html")]
    [InlineData("/docs/page.html", "/docs/page.html", "docs/page.html")]
    public void GenerateWithSource_FullOverrideDottedUrl_OnlyWebExtensionIsFile(string url, string expectedUrl, string expectedOutput)
    {
        var document = CreateDocument(
            "dotted",
            new Dictionary<string, object>
            {
                ["collection"] = "posts",
                ["route"] = new Dictionary<string, object>
                {
                    ["url"] = url,
                    ["template"] = "pages/full.html"
                }
            });

        var result = RouteGenerator.GenerateWithSource(document);

        Assert.Equal(RouteGenerator.RouteSource.FullOverride, result.Source);
        Assert.Equal(new RouteInfo(expectedUrl, expectedOutput, "pages/full.html"), result.Route);
    }

    [Fact]
    public void Generate_WithNonWebExtensionPermalink_RemainsDirectoryAndConsistent()
    {
        var document = CreateDocument(
            "app",
            new Dictionary<string, object> { ["collection"] = "posts" });
        var collections = new Dictionary<string, RouteGenerator.CollectionRouteRule>
        {
            ["posts"] = new("/download/{slug}.php", "pages/post.html")
        };

        var route = RouteGenerator.Generate(document, collections: collections);

        Assert.Equal("/download/app.php/", route.Url);
        Assert.Equal("download/app.php/index.html", route.OutputPath);
        Assert.Equal(route.Url, RoutePathBuilder.NormalizeUrl(route.Url));
    }

    [Theory]
    [InlineData("/files/{slug}.pdf")]
    [InlineData("/assets/{slug}.css")]
    [InlineData("/js/{slug}.JS")]
    [InlineData("/img/{slug}.png")]
    public void Generate_WithStaticAssetPermalink_ThrowsWithStaticHint(string pattern)
    {
        var document = CreateDocument(
            "report",
            new Dictionary<string, object> { ["collection"] = "posts" });
        var collections = new Dictionary<string, RouteGenerator.CollectionRouteRule>
        {
            ["posts"] = new(pattern, "pages/post.html")
        };

        var ex = Assert.Throws<ConfigException>(() => RouteGenerator.Generate(document, collections: collections));

        Assert.Equal(DiagnosticCode.RouteInvalidPattern, ex.Code);
        Assert.Contains("static/", ex.Message, StringComparison.Ordinal);
        Assert.Contains("assets/", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_WithStaticAssetSitePermalink_Throws()
    {
        var document = CreateDocument("report", new Dictionary<string, object> { ["collection"] = "posts" });
        var permalinks = new Dictionary<string, string> { ["post"] = "/files/{slug}.zip" };

        var ex = Assert.Throws<ConfigException>(() => RouteGenerator.Generate(document, permalinks: permalinks));

        Assert.Equal(DiagnosticCode.RouteInvalidPattern, ex.Code);
    }

    [Theory]
    [InlineData("/files/report.pdf")]
    [InlineData("/app.js")]
    [InlineData("/theme/site.CSS")]
    [InlineData("/media/intro.mp4")]
    public void GenerateWithSource_FullOverrideStaticAssetUrl_ThrowsWithStaticHint(string url)
    {
        var document = CreateDocument(
            "report",
            new Dictionary<string, object>
            {
                ["collection"] = "posts",
                ["route"] = new Dictionary<string, object> { ["url"] = url, ["template"] = "pages/full.html" }
            });

        var ex = Assert.Throws<ConfigException>(() => RouteGenerator.GenerateWithSource(document));

        Assert.Equal(DiagnosticCode.RouteInvalidInternalUrl, ex.Code);
        Assert.Contains(url, ex.Message, StringComparison.Ordinal);
        Assert.Contains("static/", ex.Message, StringComparison.Ordinal);
        Assert.Contains("assets/", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_PartialOverrideStaticAssetUrl_Throws()
    {
        var document = CreateDocument(
            "report",
            new Dictionary<string, object>
            {
                ["collection"] = "posts",
                ["route"] = new Dictionary<string, object> { ["url"] = "/downloads/report.pdf" }
            });
        var collections = new Dictionary<string, RouteGenerator.CollectionRouteRule>
        {
            ["posts"] = new("/blog/{slug}/", "pages/post.html")
        };

        var ex = Assert.Throws<ConfigException>(() => RouteGenerator.Generate(document, collections: collections));

        Assert.Equal(DiagnosticCode.RouteInvalidInternalUrl, ex.Code);
    }

    // Not static assets: trailing slash means a page directory, slugs never trigger, unknown dots stay directories.
    [Theory]
    [InlineData("/docs/node.js/", "/docs/node.js/", "docs/node.js/index.html")]
    [InlineData("/docs/v1.0", "/docs/v1.0/", "docs/v1.0/index.html")]
    [InlineData("/download/app.php", "/download/app.php/", "download/app.php/index.html")]
    public void GenerateWithSource_FullOverrideNonAssetDottedUrl_IsAllowed(string url, string expectedUrl, string expectedOutput)
    {
        var document = CreateDocument(
            "page",
            new Dictionary<string, object>
            {
                ["collection"] = "posts",
                ["route"] = new Dictionary<string, object> { ["url"] = url, ["template"] = "pages/full.html" }
            });

        var result = RouteGenerator.GenerateWithSource(document);

        Assert.Equal(new RouteInfo(expectedUrl, expectedOutput, "pages/full.html"), result.Route);
    }

    [Fact]
    public void Generate_DirectoryPermalinkWithAssetLikeSlug_IsAllowed()
    {
        var document = CreateDocument("node.js", new Dictionary<string, object> { ["collection"] = "posts" });
        var collections = new Dictionary<string, RouteGenerator.CollectionRouteRule>
        {
            ["posts"] = new("/blog/{slug}", "pages/post.html")
        };

        var route = RouteGenerator.Generate(document, collections: collections);

        Assert.Equal("/blog/node.js/", route.Url);
        Assert.Equal("blog/node.js/index.html", route.OutputPath);
    }

    [Fact]
    public void GenerateWithSource_PartialOverrideDottedUrl_RemainsDirectory()
    {
        var document = CreateDocument(
            "dotted",
            new Dictionary<string, object>
            {
                ["collection"] = "posts",
                ["route"] = new Dictionary<string, object>
                {
                    ["url"] = "/docs/v2.1"
                }
            });
        var collections = new Dictionary<string, RouteGenerator.CollectionRouteRule>
        {
            ["posts"] = new("/blog/{slug}/", "pages/post.html")
        };

        var route = RouteGenerator.Generate(document, collections: collections);

        Assert.Equal("/docs/v2.1/", route.Url);
        Assert.Equal("docs/v2.1/index.html", route.OutputPath);
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
    public void GenerateWithSource_PartialOverrideWithRelativeUrl_Throws()
    {
        var document = CreateDocument(
            "partial-relative",
            new Dictionary<string, object>
            {
                ["collection"] = "posts",
                ["route"] = new Dictionary<string, object>
                {
                    ["url"] = "partial-relative"
                }
            });
        var collections = new Dictionary<string, RouteGenerator.CollectionRouteRule>
        {
            ["posts"] = new("/blog/{slug}/", "pages/post.html")
        };

        var exception = Assert.Throws<ConfigException>(() =>
            RouteGenerator.GenerateWithSource(document, collections: collections));

        Assert.Equal(DiagnosticCode.RouteInvalidInternalUrl, exception.Code);
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
    public void GenerateWithSource_CollectionRuleWithRelativePermalink_Throws()
    {
        var document = CreateDocument(
            "collection-relative",
            new Dictionary<string, object>
            {
                ["collection"] = "posts"
            });
        var collections = new Dictionary<string, RouteGenerator.CollectionRouteRule>
        {
            ["posts"] = new("blog/{slug}/", "pages/post.html")
        };

        var exception = Assert.Throws<ConfigException>(() =>
            RouteGenerator.GenerateWithSource(document, collections: collections));

        Assert.Equal(DiagnosticCode.RouteInvalidInternalUrl, exception.Code);
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
        string contentType =
            fieldValues.TryGetValue("type", out object? typeValue) &&
            typeValue is string type
                ? type
                : "post";
        string collection =
            fieldValues.TryGetValue("collection", out object? collectionValue) &&
            collectionValue is string collectionName
                ? collectionName
                : "posts";
        var record = new ContentRecord(
            new ContentIdentity(
                slug,
                slug,
                slug,
                contentType,
                "published"),
            new ContentPresentation(
                slug,
                null,
                null,
                "en",
                Array.Empty<string>()),
            new ContentClassification(
                contentType,
                collection,
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
