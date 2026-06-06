using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Engine.Plugins;
using Bukit.Engine.Plugins.BuiltIn;
using Bukit.Routing;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Shared;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class SearchIndexPluginExtendedTests
{
    private static ContentItem CreateItem(string id, string title, string slug, string? contentHtml = null)
    {
        return new ContentItem(
            id,
            title,
            slug,
            DateTimeOffset.UtcNow,
            contentHtml,
            new Dictionary<string, object>(),
            null,
            null);
    }

    private static RouteInfo CreateRoute(string url, string outputPath)
    {
        return new RouteInfo(url, outputPath, "post");
    }

    private static SeoIndexEntry CreateSeoEntry(RouteInfo route, bool indexable = true)
    {
        return new SeoIndexEntry(route, route.Url, null, indexable, DateTimeOffset.UtcNow, null, null);
    }

    private static ContentDocument SearchDocument(string id, string title, bool excludeFromSearch)
    {
        var record = new ContentRecord(
            new ContentIdentity(id, id, id, "post", "published"),
            new ContentPresentation(title, null, $"<p>{title}</p>", "en", []),
            new ContentClassification("post", "post", [], []),
            new ContentOwnership(null, null, null, null),
            new ContentLifecycle(DateTimeOffset.UnixEpoch, null, null, null),
            new ProvenanceRecord("markdown", null, [], [], null),
            new TrustMetadata(null, "approved", []),
            [],
            [],
            []);

        return new ContentDocument(
            record,
            new ContentBodyRef($"<p>{title}</p>", null, null, null),
            new ContentRoutePolicy(null, null, null, null, "post"),
            new ContentPublishPolicy(false, false, false, false, excludeFromSearch, false, false),
            new Dictionary<string, ContentField>(),
            Array.Empty<ContentDiagnostic>());
    }

    [Fact]
    public void AfterBuild_StandardIndex_GeneratesFile()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "bukit_search_std_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var config = new AppConfig
            {
                Site = new SiteConfig
                {
                    Name = "test",
                    Title = "Test Site",
                    BaseUrl = "https://example.com"
                },
                Content = new ContentConfig { Provider = "markdown" }
            };

            var route1 = CreateRoute("/", "index.html");
            var route2 = CreateRoute("/about", "about/index.html");
            var seoIndex = new Dictionary<string, SeoIndexEntry>(StringComparer.OrdinalIgnoreCase)
            {
                ["index.html"] = CreateSeoEntry(route1),
                ["about/index.html"] = CreateSeoEntry(route2),
            };

            var context = new BuildContext
            {
                Config = config,
                RootDir = tempDir,
                OutputDir = tempDir,
                BaseUrl = "https://example.com",
                LayoutsDir = tempDir,
                Routed = new List<(ContentItem, RouteInfo)>
                {
                    (CreateItem("1", "Home", "home", "<p>Welcome</p>"), route1),
                    (CreateItem("2", "About", "about", "<p>About us</p>"), route2),
                },
                BodyStore = NullContentBodyStore.Instance,
                Logger = new ConsoleLogger(LogLevel.Debug),
            };
            context.SeoIndex = seoIndex;

            var plugin = new SearchIndexPlugin();
            plugin.AfterBuild(context);

            var indexPath = Path.Combine(tempDir, "search.json");
            Assert.True(File.Exists(indexPath));
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
    public void AfterBuild_WithRoutedDocuments_UsesTypedSearchProjection()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "bukit_search_docs_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var route1 = CreateRoute("/included/", "included/index.html");
            var route2 = CreateRoute("/excluded/", "excluded/index.html");
            var context = new BuildContext
            {
                Config = new AppConfig
                {
                    Site = new SiteConfig
                    {
                        Name = "test",
                        Title = "Test Site",
                        BaseUrl = "https://example.com"
                    },
                    Content = new ContentConfig { Provider = "markdown" }
                },
                RootDir = tempDir,
                OutputDir = tempDir,
                BaseUrl = "https://example.com",
                LayoutsDir = tempDir,
                Routed = Array.Empty<(ContentItem, RouteInfo)>(),
                RoutedDocuments = new[]
                {
                    (SearchDocument("included", "Included", excludeFromSearch: false), route1),
                    (SearchDocument("excluded", "Excluded", excludeFromSearch: true), route2)
                },
                BodyStore = NullContentBodyStore.Instance,
                Logger = new ConsoleLogger(LogLevel.Debug),
                SeoIndex = new Dictionary<string, SeoIndexEntry>(StringComparer.OrdinalIgnoreCase)
                {
                    ["included/index.html"] = CreateSeoEntry(route1),
                    ["excluded/index.html"] = CreateSeoEntry(route2)
                }
            };

            var plugin = new SearchIndexPlugin();
            plugin.AfterBuild(context);

            var json = File.ReadAllText(Path.Combine(tempDir, "search.json"));
            Assert.Contains("included", json, StringComparison.Ordinal);
            Assert.DoesNotContain("excluded", json, StringComparison.Ordinal);
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
    public void AfterBuild_WithDerivedItems_IncludesWhenSearchIncludeDerived()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "bukit_search_derived_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var config = new AppConfig
            {
                Site = new SiteConfig
                {
                    Name = "test",
                    Title = "Derived Test",
                    BaseUrl = "https://example.com",
                    SearchIncludeDerived = true
                },
                Content = new ContentConfig { Provider = "markdown" }
            };

            var mainRoute = CreateRoute("/main", "main/index.html");
            var derivedRoute = CreateRoute("/derived", "derived/index.html");
            var seoIndex = new Dictionary<string, SeoIndexEntry>(StringComparer.OrdinalIgnoreCase)
            {
                ["main/index.html"] = CreateSeoEntry(mainRoute),
                ["derived/index.html"] = CreateSeoEntry(derivedRoute),
            };

            var context = new BuildContext
            {
                Config = config,
                RootDir = tempDir,
                OutputDir = tempDir,
                BaseUrl = "https://example.com",
                LayoutsDir = tempDir,
                Routed = new List<(ContentItem, RouteInfo)>
                {
                    (CreateItem("1", "Main", "main", "<p>Main content</p>"), mainRoute),
                },
                BodyStore = NullContentBodyStore.Instance,
                Logger = new ConsoleLogger(LogLevel.Debug),
            };
            context.SeoIndex = seoIndex;
            context.DerivedRouted.Add((CreateItem("d1", "Derived Item", "derived", "<p>Derived content</p>"), derivedRoute));
            context.DerivedRoutes.Add((derivedRoute, DateTimeOffset.UtcNow));

            var plugin = new SearchIndexPlugin();
            plugin.AfterBuild(context);

            var indexPath = Path.Combine(tempDir, "search.json");
            Assert.True(File.Exists(indexPath));
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
    public void AfterBuild_WithoutDerived_FlagOff()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "bukit_search_noderived_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var config = new AppConfig
            {
                Site = new SiteConfig
                {
                    Name = "test",
                    Title = "No Derived",
                    BaseUrl = "https://example.com",
                    SearchIncludeDerived = false
                },
                Content = new ContentConfig { Provider = "markdown" }
            };

            var mainRoute = CreateRoute("/main", "main/index.html");
            var derivedRoute = CreateRoute("/derived", "derived/index.html");
            var seoIndex = new Dictionary<string, SeoIndexEntry>(StringComparer.OrdinalIgnoreCase)
            {
                ["main/index.html"] = CreateSeoEntry(mainRoute),
            };

            var context = new BuildContext
            {
                Config = config,
                RootDir = tempDir,
                OutputDir = tempDir,
                BaseUrl = "https://example.com",
                LayoutsDir = tempDir,
                Routed = new List<(ContentItem, RouteInfo)>
                {
                    (CreateItem("1", "Main", "main", "<p>Main content</p>"), mainRoute),
                },
                BodyStore = NullContentBodyStore.Instance,
                Logger = new ConsoleLogger(LogLevel.Debug),
            };
            context.SeoIndex = seoIndex;
            context.DerivedRouted.Add((CreateItem("d1", "Derived Item", "derived", "<p>Derived content</p>"), derivedRoute));

            var plugin = new SearchIndexPlugin();
            plugin.AfterBuild(context);

            var indexPath = Path.Combine(tempDir, "search.json");
            Assert.True(File.Exists(indexPath));
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
    public void AfterBuild_EmptyRouted_DoesNotThrow()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "bukit_search_empty_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var config = new AppConfig
            {
                Site = new SiteConfig
                {
                    Name = "test",
                    Title = "Empty",
                    BaseUrl = "https://example.com"
                },
                Content = new ContentConfig { Provider = "markdown" }
            };

            var context = new BuildContext
            {
                Config = config,
                RootDir = tempDir,
                OutputDir = tempDir,
                BaseUrl = "https://example.com",
                LayoutsDir = tempDir,
                Routed = Array.Empty<(ContentItem, RouteInfo)>(),
                BodyStore = NullContentBodyStore.Instance,
                Logger = new ConsoleLogger(LogLevel.Debug),
            };
            context.SeoIndex = new Dictionary<string, SeoIndexEntry>();

            var plugin = new SearchIndexPlugin();
            plugin.AfterBuild(context);

            var indexPath = Path.Combine(tempDir, "search.json");
            Assert.True(File.Exists(indexPath));
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }
}
