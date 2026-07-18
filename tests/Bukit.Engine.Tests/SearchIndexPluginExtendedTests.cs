using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Engine.Plugins;
using Bukit.Engine.Plugins.BuiltIn;
using Bukit.Rendering;
using Bukit.Routing;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Shared;
using System.Text.Json;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class SearchIndexPluginExtendedTests
{
    private static ContentDocument CreateItem(string id, string title, string slug, string? contentHtml = null)
    {
        return ContentDocument.Create(
            id,
            title,
            slug,
            DateTimeOffset.UtcNow,
            contentHtml,
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

    private static string RenderSearchUi(SearchDetailConfig searchConfig)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "bukit_search_ui_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            SearchIndexPlugin.WriteSearchUi(
                new AppConfig
                {
                    Site = new SiteConfig
                    {
                        Name = "test",
                        Title = "Test Site",
                        Search = searchConfig
                    },
                    Content = TestContent.Markdown()
                },
                tempDir);

            return File.ReadAllText(Path.Combine(tempDir, "bukit-search.html"));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void WriteSearchUi_DoesNotUseInnerHtmlForDynamicResults()
    {
        var searchUi = RenderSearchUi(new SearchDetailConfig());

        Assert.DoesNotContain(".innerHTML", searchUi, StringComparison.Ordinal);
    }

    [Fact]
    public void WriteSearchUi_UsesTextNodesForTitleSnippetAndMark()
    {
        var searchUi = RenderSearchUi(new SearchDetailConfig());

        Assert.Contains("results.replaceChildren();", searchUi, StringComparison.Ordinal);
        Assert.Contains("document.createTextNode(value)", searchUi, StringComparison.Ordinal);
        Assert.Contains("document.createElement('mark')", searchUi, StringComparison.Ordinal);
        Assert.Contains("mark.textContent=match[0]", searchUi, StringComparison.Ordinal);
        Assert.Contains("appendHighlighted(strong,it.title||'Untitled',q);", searchUi, StringComparison.Ordinal);
        Assert.Contains("appendHighlighted(small,it.snippet,q);", searchUi, StringComparison.Ordinal);
    }

    [Fact]
    public void WriteSearchUi_EncodesMaliciousPlaceholder()
    {
        var searchUi = RenderSearchUi(new SearchDetailConfig
        {
            PlaceholderText = "\" autofocus onfocus=\"alert(1)\""
        });

        Assert.Contains(
            "placeholder=\"&quot; autofocus onfocus=&quot;alert(1)&quot;\"",
            searchUi,
            StringComparison.Ordinal);
        Assert.DoesNotContain("placeholder=\"\" autofocus", searchUi, StringComparison.Ordinal);
    }

    [Fact]
    public void SearchUi_MaliciousTitleAndSnippetRemainDataWithoutInterpretingSink()
    {
        const string titlePayload = "<img src=x onerror=alert('title')>";
        const string snippetPayload = "<svg onload=alert('snippet')>";
        var tempDir = Path.Combine(Path.GetTempPath(), "bukit_search_payload_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var document = ContentDocument.Create(
                "malicious",
                titlePayload,
                "malicious",
                DateTimeOffset.UtcNow,
                "<p>Body</p>",
                ContentFieldReader.ToFieldMap(new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                {
                    ["summary"] = snippetPayload
                }));
            var route = CreateRoute("/malicious/", "malicious/index.html");

            SearchIndexBuilder.GenerateSingleSearchIndex(
                tempDir,
                "/",
                includeDerived: false,
                emitSnippet: true,
                new[] { new RoutedContentDocument(document.ToDocument(), route) },
                Array.Empty<RoutedContentDocument>(),
                new Dictionary<string, SeoIndexEntry>(StringComparer.OrdinalIgnoreCase)
                {
                    [route.OutputPath] = CreateSeoEntry(route)
                },
                NullContentBodyStore.Instance);
            var searchUi = RenderSearchUi(new SearchDetailConfig());

            using var searchIndex = JsonDocument.Parse(File.ReadAllText(Path.Combine(tempDir, "search.json")));
            var item = Assert.Single(searchIndex.RootElement.EnumerateArray());
            Assert.Equal(titlePayload, item.GetProperty("title").GetString());
            Assert.Equal(snippetPayload, item.GetProperty("snippet").GetString());
            Assert.DoesNotContain(".innerHTML", searchUi, StringComparison.Ordinal);
            Assert.DoesNotContain("insertAdjacentHTML", searchUi, StringComparison.Ordinal);
            Assert.DoesNotContain(".outerHTML", searchUi, StringComparison.Ordinal);
            Assert.DoesNotContain("document.write", searchUi, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
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
                Content = TestContent.Markdown()
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
                RoutedDocuments = new List<(ContentDocument, RouteInfo)>
                {
                    (CreateItem("1", "Home", "home", "<p>Welcome</p>"), route1),
                    (CreateItem("2", "About", "about", "<p>About us</p>"), route2),
                }.ToRoutedDocuments(),
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
                Content = TestContent.Markdown()
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
                RoutedDocuments = new List<(ContentDocument, RouteInfo)>
                {
                    (CreateItem("1", "Main", "main", "<p>Main content</p>"), mainRoute),
                }.ToRoutedDocuments(),
                BodyStore = NullContentBodyStore.Instance,
                Logger = new ConsoleLogger(LogLevel.Debug),
            };
            context.SeoIndex = seoIndex;
            context.DerivedDocuments.Add(new RoutedContentDocument(CreateItem("d1", "Derived Item", "derived", "<p>Derived content</p>").ToDocument(), derivedRoute));
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
                Content = TestContent.Markdown()
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
                RoutedDocuments = new List<(ContentDocument, RouteInfo)>
                {
                    (CreateItem("1", "Main", "main", "<p>Main content</p>"), mainRoute),
                }.ToRoutedDocuments(),
                BodyStore = NullContentBodyStore.Instance,
                Logger = new ConsoleLogger(LogLevel.Debug),
            };
            context.SeoIndex = seoIndex;
            context.DerivedDocuments.Add(new RoutedContentDocument(CreateItem("d1", "Derived Item", "derived", "<p>Derived content</p>").ToDocument(), derivedRoute));

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
                Content = TestContent.Markdown()
            };

            var context = new BuildContext
            {
                Config = config,
                RootDir = tempDir,
                OutputDir = tempDir,
                BaseUrl = "https://example.com",
                LayoutsDir = tempDir,
                RoutedDocuments = Array.Empty<RoutedContentDocument>(),
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

    [Fact]
    public void AfterBuild_WithGraphOnlyListRoute_UsesSeoModelForSearchItem()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "bukit_search_list_seo_" + Guid.NewGuid().ToString("N"));
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
                Content = TestContent.Markdown()
            };

            var route = new ListRoutePlan
            {
                RouteId = "filter:companies:country:malaysia:1",
                Kind = ListRouteKind.FilteredListPage,
                Url = "/companies/malaysia/",
                OutputPath = "companies/malaysia/index.html",
                Template = "pages/company-list.html",
                Collection = "companies",
                PageNumber = 1,
                PageSize = 10,
                TotalItems = 1,
                CanonicalUrl = "/companies/malaysia/",
                FilterContext = new ListRouteFilterContext
                {
                    Field = "country",
                    Operator = "equals",
                    Value = "malaysia",
                    Values = new[] { "malaysia" }
                },
                Items = new[]
                {
                    new ListRouteItem
                    {
                        Id = "company-1",
                        Title = "Acme Malaysia",
                        Url = "/companies/acme/",
                        OutputPath = "companies/acme/index.html",
                        Summary = "Regional logistics partner"
                    }
                }
            };
            var routeInfo = route.ToRouteInfo();
            var context = new BuildContext
            {
                Config = config,
                RootDir = tempDir,
                OutputDir = tempDir,
                BaseUrl = "/",
                LayoutsDir = tempDir,
                RoutedDocuments = Array.Empty<RoutedContentDocument>(),
                BodyStore = NullContentBodyStore.Instance,
                Logger = new ConsoleLogger(LogLevel.Debug),
            };
            context.SeoIndex = new Dictionary<string, SeoIndexEntry>(StringComparer.OrdinalIgnoreCase)
            {
                [route.OutputPath] = new(routeInfo, "https://example.com/companies/malaysia/", null, true, DateTimeOffset.UtcNow, null, "list", IsDerived: true)
            };
            context.Data[ListRouteGraphBuilder.BuildContextDataKey] = ListRouteGraph.Create(new[] { route });
            context.Data[BuildContextDataKeys.SeoModels] = new Dictionary<string, SeoModel>(StringComparer.OrdinalIgnoreCase)
            {
                [route.OutputPath] = new()
                {
                    Title = "Malaysia Companies",
                    Description = "Companies operating in Malaysia",
                    Canonical = "https://example.com/companies/malaysia/"
                }
            };

            var plugin = new SearchIndexPlugin();
            plugin.AfterBuild(context);

            using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(tempDir, "search.json")));
            var item = Assert.Single(doc.RootElement.EnumerateArray());
            Assert.Equal("Malaysia Companies", item.GetProperty("title").GetString());
            Assert.Equal("Companies operating in Malaysia", item.GetProperty("summary").GetString());
            Assert.Contains("Companies operating in Malaysia", item.GetProperty("content").GetString(), StringComparison.Ordinal);
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
