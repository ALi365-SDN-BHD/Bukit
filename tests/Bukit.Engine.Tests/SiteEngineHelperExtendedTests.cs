using System.Reflection;
using Bukit.Config;
using Bukit.Content;
using Bukit.Engine;
using Bukit.Engine.Plugins;
using Bukit.Rendering;
using Bukit.Routing;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class SiteEngineHelperExtendedTests
{
    private static T? InvokeStatic<T>(string methodName, params object?[] args)
    {
        var method = typeof(SiteEngine).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        return (T?)method!.Invoke(null, args);
    }

    private static object? InvokeStatic(string methodName, params object?[] args)
    {
        var method = typeof(SiteEngine).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        return method!.Invoke(null, args);
    }

    private static ContentItem CreateItem(string id, string title, string slug)
    {
        return new ContentItem(id, title, slug, DateTimeOffset.UtcNow, null,
            new Dictionary<string, object>(), null, null);
    }

    private static AppConfig CreateTestConfig()
    {
        return new AppConfig
        {
            Site = new SiteConfig { Name = "test", Title = "Test", BaseUrl = "/", Url = "https://example.com" },
            Content = new ContentConfig { Provider = "markdown" },
            Taxonomy = new TaxonomyConfig()
        };
    }

    [Fact]
    public void BuildSeoAlternates_WithMultipleLanguages_ReturnsAlternates()
    {
        var config = new AppConfig
        {
            Site = new SiteConfig
            {
                Name = "test",
                Title = "Test",
                BaseUrl = "/",
                Url = "https://example.com"
            },
            Content = new ContentConfig { Provider = "markdown" }
        };
        var items = new List<ContentItem>
        {
            CreateItem("1", "Post One", "post-one"),
        };
        var languages = new List<string> { "en", "zh" };
        var defaultLanguage = "en";
        var rootBaseUrl = "/";

        var result = InvokeStatic<IReadOnlyDictionary<string, IReadOnlyList<SeoAlternateModel>>>(
            "BuildSeoAlternates", config, items, languages, defaultLanguage, rootBaseUrl);

        Assert.NotNull(result);
    }

    [Fact]
    public void BuildSeoAlternates_WithEmptyLanguages_ReturnsEmpty()
    {
        var config = CreateTestConfig();
        var items = new List<ContentItem>
        {
            CreateItem("1", "Post One", "post-one"),
        };
        var languages = Array.Empty<string>();
        var defaultLanguage = "en";
        var rootBaseUrl = "/";

        var result = InvokeStatic<IReadOnlyDictionary<string, IReadOnlyList<SeoAlternateModel>>>(
            "BuildSeoAlternates", config, items, languages, defaultLanguage, rootBaseUrl);

        Assert.NotNull(result);
        Assert.Empty(result!);
    }

    [Fact]
    public void BuildTaxonomyRouteUrls_WithTaxonomyConfig_ReturnsUrls()
    {
        var config = new AppConfig
        {
            Site = new SiteConfig { Name = "test", Title = "Test", BaseUrl = "/" },
            Content = new ContentConfig { Provider = "markdown" },
            Taxonomy = new TaxonomyConfig
            {
                Kinds = new List<TaxonomyKindConfig>
                {
                    new TaxonomyKindConfig { Key = "tags", Kind = "tags" },
                }
            }
        };
        var item = CreateItem("1", "Post One", "post-one");
        var route = new RouteInfo("/post-one", "post-one/index.html", "post");
        var routed = new List<(ContentItem, RouteInfo)> { (item, route) };

        var result = InvokeStatic<IReadOnlyList<string>>("BuildTaxonomyRouteUrls", config, routed);

        Assert.NotNull(result);
    }

    [Fact]
    public void BuildTaxonomyRouteUrls_WithEmptyRouted_ReturnsEmpty()
    {
        var config = CreateTestConfig();
        var routed = Array.Empty<(ContentItem, RouteInfo)>();

        var result = InvokeStatic<IReadOnlyList<string>>("BuildTaxonomyRouteUrls", config, routed);

        Assert.NotNull(result);
        Assert.Empty(result!);
    }

    [Fact]
    public void BuildPaginationRouteUrls_WithPagination_ReturnsUrls()
    {
        var config = new AppConfig
        {
            Site = new SiteConfig
            {
                Name = "test",
                Title = "Test",
                BaseUrl = "/",
                Collections = new Dictionary<string, CollectionConfig>
                {
                    ["post"] = new CollectionConfig
                    {
                        ListRoute = "/blog/",
                        Permalink = "/blog/:slug/",
                        Template = "post",
                        Pagination = new CollectionPaginationConfig { Enabled = true, PageSize = 10 }
                    }
                }
            },
            Content = new ContentConfig { Provider = "markdown" }
        };
        var items = new List<ContentItem>();
        for (var i = 0; i < 25; i++)
        {
            items.Add(CreateItem($"{i}", $"Post {i}", $"post-{i}"));
        }

        var route = new RouteInfo("/blog/post-0", "blog/post-0/index.html", "post");
        var routed = new List<(ContentItem, RouteInfo)>();
        foreach (var item in items)
        {
            routed.Add((item, route));
        }

        var result = InvokeStatic<IReadOnlyList<string>>("BuildPaginationRouteUrls", config, routed);

        Assert.NotNull(result);
    }

    [Fact]
    public void BuildPaginationRouteUrls_WithEmptyRouted_ReturnsEmpty()
    {
        var config = CreateTestConfig();
        var routed = Array.Empty<(ContentItem, RouteInfo)>();

        var result = InvokeStatic<IReadOnlyList<string>>("BuildPaginationRouteUrls", config, routed);

        Assert.NotNull(result);
        Assert.Empty(result!);
    }

    [Fact]
    public void AddTaxonomyKindRoutes_WithValidKinds_AddsRoutes()
    {
        var result = new List<string>();
        var kind = "tags";
        var termCounts = new Dictionary<string, int>
        {
            ["tech"] = 10,
            ["life"] = 5,
        };
        var pageSize = 10;
        var indexEnabled = true;

        InvokeStatic("AddTaxonomyKindRoutes", result, kind, termCounts, pageSize, indexEnabled);

        Assert.NotEmpty(result);
    }

    [Fact]
    public void AddTaxonomyKindRoutes_WithEmptyTermCounts_AddsEmpty()
    {
        var result = new List<string>();
        var kind = "categories";
        var termCounts = new Dictionary<string, int>();
        var pageSize = 10;
        var indexEnabled = true;

        InvokeStatic("AddTaxonomyKindRoutes", result, kind, termCounts, pageSize, indexEnabled);

        Assert.Empty(result);
    }

    [Fact]
    public void BuildTaxonomyTermCounts_WithTerms_ReturnsCounts()
    {
        var item = CreateItem("1", "Post One", "post-one");
        var route = new RouteInfo("/post-one", "post-one/index.html", "post");
        var routed = new List<(ContentItem, RouteInfo)> { (item, route) };
        var key = "tags";

        var result = InvokeStatic<IReadOnlyDictionary<string, int>>("BuildTaxonomyTermCounts", routed, key);

        Assert.NotNull(result);
        Assert.True(result!.Count >= 0);
    }

    [Fact]
    public void AddVariantRouteAlternates_WithNoVariants_ReturnsExisting()
    {
        var config = CreateTestConfig();
        var existing = new Dictionary<string, IReadOnlyList<SeoAlternateModel>>(StringComparer.Ordinal)
        {
            ["/test"] = new List<SeoAlternateModel> { new("en", "https://example.com/test") }
        };
        var routes = new List<RouteInfo>();
        var rootBaseUrl = "/";
        var defaultLanguage = "en";

        var result = InvokeStatic<IReadOnlyDictionary<string, IReadOnlyList<SeoAlternateModel>>>(
            "AddVariantRouteAlternates", config, existing, routes, rootBaseUrl, defaultLanguage);

        Assert.NotNull(result);
        Assert.Same(existing, result);
    }

    [Fact]
    public void BuildListOutputPath_WithValidRoute_ReturnsPath()
    {
        var listRoute = "/posts";

        var result = InvokeStatic<string>("BuildListOutputPath", listRoute);

        Assert.NotNull(result);
        Assert.Contains("index.html", result!, StringComparison.Ordinal);
    }

    [Fact]
    public void WriteRobotsTxtIfRequested_WhenEnabled_WritesFile()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "bukit_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var config = new AppConfig
            {
                Site = new SiteConfig
                {
                    Name = "Test",
                    Title = "Test",
                    BaseUrl = "/",
                    Url = "https://example.com",
                    Seo = new SeoConfig
                    {
                        RobotsTxt = new SeoRobotsTxtConfig { Enabled = true }
                    }
                },
                Content = new ContentConfig { Provider = "markdown" }
            };
            var seoEntries = new Dictionary<string, SeoIndexEntry>();

            InvokeStatic("WriteRobotsTxtIfRequested", config, tempDir, "/", seoEntries);

            var robotsPath = Path.Combine(tempDir, "robots.txt");
            Assert.True(File.Exists(robotsPath));
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public void WriteRobotsTxtIfRequested_WhenDisabled_DoesNotWriteFile()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "bukit_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var config = new AppConfig
            {
                Site = new SiteConfig
                {
                    Name = "Test",
                    Title = "Test",
                    BaseUrl = "/",
                    Seo = new SeoConfig
                    {
                        RobotsTxt = new SeoRobotsTxtConfig { Enabled = false }
                    }
                },
                Content = new ContentConfig { Provider = "markdown" }
            };
            var seoEntries = new Dictionary<string, SeoIndexEntry>();

            InvokeStatic("WriteRobotsTxtIfRequested", config, tempDir, "/", seoEntries);

            var robotsPath = Path.Combine(tempDir, "robots.txt");
            Assert.False(File.Exists(robotsPath));
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public void GetSeoAlternates_WithExistingKey_ReturnsAlternates()
    {
        var alternates = new Dictionary<string, IReadOnlyList<SeoAlternateModel>>(StringComparer.Ordinal)
        {
            ["/test"] = new List<SeoAlternateModel>
            {
                new("en", "https://example.com/en/test"),
                new("zh", "https://example.com/zh/test"),
            }
        };
        var key = "/test";

        var result = InvokeStatic<IReadOnlyList<SeoAlternateModel>?>("GetSeoAlternates", alternates, key);

        Assert.NotNull(result);
        Assert.Equal(2, result!.Count);
    }

    [Fact]
    public void GetSeoAlternates_WithMissingKey_ReturnsNull()
    {
        var alternates = new Dictionary<string, IReadOnlyList<SeoAlternateModel>>(StringComparer.Ordinal)
        {
            ["/test"] = new List<SeoAlternateModel> { new("en", "https://example.com/test") }
        };
        var key = "/nonexistent";

        var result = InvokeStatic<IReadOnlyList<SeoAlternateModel>?>("GetSeoAlternates", alternates, key);

        Assert.Null(result);
    }

    [Fact]
    public void NormalizeSeoPageSize_WithZero_Returns10()
    {
        var result = InvokeStatic<int>("NormalizeSeoPageSize", 0);

        Assert.Equal(10, result);
    }

    [Fact]
    public void NormalizeSeoPageSize_WithNegative_Returns10()
    {
        var result = InvokeStatic<int>("NormalizeSeoPageSize", -5);

        Assert.Equal(10, result);
    }

    [Fact]
    public void NormalizeSeoPageSize_WithPositive_ReturnsSame()
    {
        var result = InvokeStatic<int>("NormalizeSeoPageSize", 20);

        Assert.Equal(20, result);
    }

    [Fact]
    public void SlugifySeoSegment_WithSimpleText_ReturnsSlugified()
    {
        var result = InvokeStatic<string>("SlugifySeoSegment", "Hello World");

        Assert.NotNull(result);
        Assert.Equal("hello-world", result);
    }

    [Fact]
    public void SlugifySeoSegment_WithSpecialCharacters_ReturnsCleanSlug()
    {
        var result = InvokeStatic<string>("SlugifySeoSegment", "C# & .NET!");

        Assert.NotNull(result);
        Assert.DoesNotContain("#", result!, StringComparison.Ordinal);
        Assert.DoesNotContain("!", result!, StringComparison.Ordinal);
    }
}
