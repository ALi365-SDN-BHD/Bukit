using System.Reflection;
using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Plugins.BuiltIn;
using Bukit.Routing;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class SiteEngineHelperTests
{
    private static readonly Type SiteEngineType = typeof(SiteEngine);
    private static readonly Type SeoServiceType = typeof(SeoAlternatesService);

    private static object InvokePrivateStatic(Type type, string methodName, params object[] args)
    {
        var method = type.GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException($"Method '{methodName}' not found on {type.Name}.");
        return method.Invoke(null, args)!;
    }

    private static T InvokePrivateStatic<T>(Type type, string methodName, params object[] args)
    {
        return (T)InvokePrivateStatic(type, methodName, args);
    }

    private static object InvokeSiteEngine(string methodName, params object[] args)
        => InvokePrivateStatic(SiteEngineType, methodName, args);

    private static T InvokeSiteEngine<T>(string methodName, params object[] args)
        => InvokePrivateStatic<T>(SiteEngineType, methodName, args);

    private static object InvokeSeoService(string methodName, params object[] args)
        => InvokePrivateStatic(SeoServiceType, methodName, args);

    private static T InvokeSeoService<T>(string methodName, params object[] args)
        => InvokePrivateStatic<T>(SeoServiceType, methodName, args);

    [Fact]
    public void SlugifySeoSegment_SimpleText_KeepsLetters()
    {
        var result = InvokeSeoService<string>("SlugifySegment", "Hello World");

        Assert.Equal("hello-world", result);
    }

    [Fact]
    public void SlugifySeoSegment_EmptyString_ReturnsEmpty()
    {
        var result = InvokeSeoService<string>("SlugifySegment", "");

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void SlugifySeoSegment_WhitespaceOnly_ReturnsEmpty()
    {
        var result = InvokeSeoService<string>("SlugifySegment", "   ");

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void SlugifySeoSegment_DotsAndUnderscores_ConvertedToDashes()
    {
        var result = InvokeSeoService<string>("SlugifySegment", "hello_world.name");

        Assert.Equal("hello-world-name", result);
    }

    [Fact]
    public void SlugifySeoSegment_LeadingTrailingDashes_Trimmed()
    {
        var result = InvokeSeoService<string>("SlugifySegment", " --test-- ");

        Assert.Equal("test", result);
    }

    [Fact]
    public void SlugifySeoSegment_SpecialCharacters_Removed()
    {
        var result = InvokeSeoService<string>("SlugifySegment", "hello!@#$%^&*()world");

        Assert.Equal("helloworld", result);
    }

    [Fact]
    public void NormalizeSeoPageSize_Negative_ReturnsTen()
    {
        var result = InvokeSeoService<int>("NormalizePageSize", -5);

        Assert.Equal(10, result);
    }

    [Fact]
    public void NormalizeSeoPageSize_Zero_ReturnsTen()
    {
        var result = InvokeSeoService<int>("NormalizePageSize", 0);

        Assert.Equal(10, result);
    }

    [Fact]
    public void NormalizeSeoPageSize_Positive_ReturnsSameValue()
    {
        var result = InvokeSeoService<int>("NormalizePageSize", 25);

        Assert.Equal(25, result);
    }

    [Fact]
    public void NormalizeListRoute_SimpleValue_AddsSlashes()
    {
        var result = RoutePathBuilder.NormalizeListRoute("blog");

        Assert.Equal("/blog/", result);
    }

    [Fact]
    public void NormalizeListRoute_AlreadyCorrect_ReturnsSame()
    {
        var result = RoutePathBuilder.NormalizeListRoute("/blog/");

        Assert.Equal("/blog/", result);
    }

    [Fact]
    public void NormalizeListRoute_Empty_ReturnsRoot()
    {
        var result = RoutePathBuilder.NormalizeListRoute("");

        Assert.Equal("/", result);
    }

    [Fact]
    public void NormalizeListRoute_Null_ReturnsRoot()
    {
        var result = RoutePathBuilder.NormalizeListRoute(null!);

        Assert.Equal("/", result);
    }

    [Fact]
    public void NormalizeListRoute_Whitespace_ReturnsRoot()
    {
        var result = RoutePathBuilder.NormalizeListRoute("   ");

        Assert.Equal("/", result);
    }

    [Fact]
    public void NormalizeListRoute_NoLeadingSlash_AddsLeadingSlash()
    {
        var result = RoutePathBuilder.NormalizeListRoute("posts");

        Assert.Equal("/posts/", result);
    }

    [Fact]
    public void NormalizeListRoute_NoTrailingSlash_AddsTrailingSlash()
    {
        var result = RoutePathBuilder.NormalizeListRoute("/posts");

        Assert.Equal("/posts/", result);
    }

    [Fact]
    public void GetSeoStringList_NullValue_ReturnsNull()
    {
        var meta = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["tags"] = default(object)!
        };

        var result = InvokeSeoService<IReadOnlyList<string>?>("GetSeoStringList", meta, "tags");

        Assert.Null(result);
    }

    [Fact]
    public void GetSeoStringList_MissingKey_ReturnsNull()
    {
        var meta = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        var result = InvokeSeoService<IReadOnlyList<string>?>("GetSeoStringList", meta, "missing");

        Assert.Null(result);
    }

    [Fact]
    public void GetSeoStringList_CommaSeparated_ReturnsParts()
    {
        var meta = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["tags"] = "alpha, beta, gamma"
        };

        var result = InvokeSeoService<IReadOnlyList<string>?>("GetSeoStringList", meta, "tags");

        Assert.NotNull(result);
        Assert.Equal(3, result!.Count);
        Assert.Equal("alpha", result[0]);
        Assert.Equal("beta", result[1]);
        Assert.Equal("gamma", result[2]);
    }

    [Fact]
    public void GetSeoStringList_SingleValue_ReturnsSingleItem()
    {
        var meta = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["tags"] = "alpha"
        };

        var result = InvokeSeoService<IReadOnlyList<string>?>("GetSeoStringList", meta, "tags");

        Assert.NotNull(result);
        Assert.Single(result!);
        Assert.Equal("alpha", result![0]);
    }

    [Fact]
    public void GetSeoStringList_EmptyString_ReturnsNull()
    {
        var meta = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["tags"] = ""
        };

        var result = InvokeSeoService<IReadOnlyList<string>?>("GetSeoStringList", meta, "tags");

        Assert.Null(result);
    }

    [Fact]
    public void GetCollection_WithCollectionMeta_ReturnsCollectionValue()
    {
        var item = new ContentItem(
            Id: "p1",
            Title: "Post",
            Slug: "post",
            PublishAt: DateTimeOffset.UtcNow,
            ContentHtml: "<p>content</p>",
            Meta: new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["collection"] = "blog"
            },
            Fields: null);

        var result = InvokeSeoService<string>("GetCollection", item);

        Assert.Equal("blog", result);
    }

    [Fact]
    public void GetCollection_WithTypeMeta_ReturnsTypeValue()
    {
        var item = new ContentItem(
            Id: "p1",
            Title: "Post",
            Slug: "post",
            PublishAt: DateTimeOffset.UtcNow,
            ContentHtml: "<p>content</p>",
            Meta: new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["type"] = "article"
            },
            Fields: null);

        var result = InvokeSeoService<string>("GetCollection", item);

        Assert.Equal("article", result);
    }

    [Fact]
    public void GetCollection_WithNeither_ReturnsPage()
    {
        var item = new ContentItem(
            Id: "p1",
            Title: "Post",
            Slug: "post",
            PublishAt: DateTimeOffset.UtcNow,
            ContentHtml: "<p>content</p>",
            Meta: new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase),
            Fields: null);

        var result = InvokeSeoService<string>("GetCollection", item);

        Assert.Equal("page", result);
    }

    [Fact]
    public void GetCollection_CollectionOverridesType()
    {
        var item = new ContentItem(
            Id: "p1",
            Title: "Post",
            Slug: "post",
            PublishAt: DateTimeOffset.UtcNow,
            ContentHtml: "<p>content</p>",
            Meta: new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["collection"] = "blog",
                ["type"] = "article"
            },
            Fields: null);

        var result = InvokeSeoService<string>("GetCollection", item);

        Assert.Equal("blog", result);
    }

    [Fact]
    public void BuildCollectionRules_NullCollections_ReturnsNull()
    {
        var site = new SiteConfig { Name = "t", Title = "t", Collections = null };

        var result = InvokeSiteEngine<IReadOnlyDictionary<string, RouteGenerator.CollectionRouteRule>?>("BuildCollectionRules", site);

        Assert.Null(result);
    }

    [Fact]
    public void BuildCollectionRules_EmptyCollections_ReturnsNull()
    {
        var site = new SiteConfig
        {
            Name = "t",
            Title = "t",
            Collections = new Dictionary<string, CollectionConfig>(StringComparer.OrdinalIgnoreCase)
        };

        var result = InvokeSiteEngine<IReadOnlyDictionary<string, RouteGenerator.CollectionRouteRule>?>("BuildCollectionRules", site);

        Assert.Null(result);
    }

    [Fact]
    public void BuildCollectionRules_WithCollections_BuildsRules()
    {
        var site = new SiteConfig
        {
            Name = "t",
            Title = "t",
            Collections = new Dictionary<string, CollectionConfig>(StringComparer.OrdinalIgnoreCase)
            {
                ["post"] = new CollectionConfig
                {
                    Permalink = "/blog/:slug/",
                    Template = "pages/post.html"
                }
            }
        };

        var result = InvokeSiteEngine<IReadOnlyDictionary<string, RouteGenerator.CollectionRouteRule>?>("BuildCollectionRules", site);

        Assert.NotNull(result);
        Assert.True(result!.ContainsKey("post"));
        Assert.Equal("/blog/:slug/", result["post"].Permalink);
        Assert.Equal("pages/post.html", result["post"].Template);
    }

    [Fact]
    public void BuildListRoutes_WithNullCollections_IncludesDefaults()
    {
        var result = SeoAlternatesService.BuildListRoutes(null!);

        Assert.Contains(result, r => r.Url == "/");
        Assert.Contains(result, r => r.Url == "/blog/");
        Assert.Contains(result, r => r.Url == "/pages/");
    }

    [Fact]
    public void BuildListRoutes_WithCollections_UsesListRoutes()
    {
        var collections = new Dictionary<string, CollectionConfig>(StringComparer.OrdinalIgnoreCase)
        {
            ["post"] = new CollectionConfig { Permalink = "/articles/:slug/", Template = "pages/post.html", ListRoute = "/articles/" },
            ["project"] = new CollectionConfig { Permalink = "/work/:slug/", Template = "pages/project.html", ListRoute = "/work/" }
        };

        var result = SeoAlternatesService.BuildListRoutes(collections);

        Assert.Contains(result, r => r.Url == "/");
        Assert.Contains(result, r => r.Url == "/articles/");
        Assert.Contains(result, r => r.Url == "/work/");
    }

    [Fact]
    public void MergeStageMetrics_AccumulatesDurationsAndCounts()
    {
        var collector = new BuildStageMetricsCollector();
        var metrics = new BuildStageMetrics(
            new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase)
            {
                ["load"] = 100,
                ["render"] = 200
            },
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["pages"] = 5,
                ["assets"] = 10
            });

        var result = InvokeSiteEngine<BuildStageMetricsCollector>("MergeStageMetrics", collector, metrics);

        Assert.Same(collector, result);
        var snapshot = collector.Snapshot();
        Assert.Equal(100, snapshot.DurationsMs["load"]);
        Assert.Equal(200, snapshot.DurationsMs["render"]);
        Assert.Equal(5, snapshot.Counts["pages"]);
        Assert.Equal(10, snapshot.Counts["assets"]);
    }

    [Fact]
    public void MergeStageMetrics_AccumulatesMultipleMetrics()
    {
        var collector = new BuildStageMetricsCollector();
        var metrics1 = new BuildStageMetrics(
            new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase) { ["load"] = 100 },
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { ["pages"] = 3 });
        var metrics2 = new BuildStageMetrics(
            new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase) { ["load"] = 50, ["render"] = 75 },
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { ["pages"] = 2, ["errors"] = 1 });

        InvokeSiteEngine<BuildStageMetricsCollector>("MergeStageMetrics", collector, metrics1);
        InvokeSiteEngine<BuildStageMetricsCollector>("MergeStageMetrics", collector, metrics2);

        var snapshot = collector.Snapshot();
        Assert.Equal(150, snapshot.DurationsMs["load"]);
        Assert.Equal(75, snapshot.DurationsMs["render"]);
        Assert.Equal(5, snapshot.Counts["pages"]);
        Assert.Equal(1, snapshot.Counts["errors"]);
    }
}
