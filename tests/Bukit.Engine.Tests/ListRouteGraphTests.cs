using System.Text.Json;
using System.Text.Json.Serialization;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Shared;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class ListRouteGraphTests
{
    [Fact]
    public void Snapshot_FromGraph_RepresentsCollectionAndTaxonomyPaginationRoutes()
    {
        var first = Item("insight-1", "Market Update", "/insights/market-update/", "Market summary");
        var second = Item("insight-2", "Policy Update", "/insights/policy-update/", "Policy summary");
        var third = Item("insight-3", "Capital Update", "/insights/capital-update/", "Capital summary");
        var items = new[] { first, second, third };

        var graph = ListRouteGraph.Create(new[]
        {
            new ListRoutePlan
            {
                RouteId = "collection:insights:1",
                Kind = ListRouteKind.CollectionList,
                Url = "/insights/",
                OutputPath = "insights/index.html",
                Template = "pages/insights-list.html",
                Collection = "insights",
                PageNumber = 1,
                PageSize = 2,
                TotalItems = 3,
                Items = items.Take(2).ToArray(),
                CanonicalUrl = "/insights/",
                NextUrl = "/insights/page/2/"
            },
            new ListRoutePlan
            {
                RouteId = "collection:insights:2",
                Kind = ListRouteKind.CollectionPage,
                Url = "/insights/page/2/",
                OutputPath = "insights/page/2/index.html",
                Template = "pages/insights-list.html",
                Collection = "insights",
                PageNumber = 2,
                PageSize = 2,
                TotalItems = 3,
                Items = items.Skip(2).ToArray(),
                CanonicalUrl = "/insights/page/2/",
                PrevUrl = "/insights/"
            },
            new ListRoutePlan
            {
                RouteId = "taxonomy:category:market:1",
                Kind = ListRouteKind.TaxonomyTermPage,
                Url = "/category/market/",
                OutputPath = "category/market/index.html",
                Template = "pages/category.html",
                Collection = "insights",
                PageNumber = 1,
                PageSize = 2,
                TotalItems = 3,
                Items = items.Take(2).ToArray(),
                CanonicalUrl = "/category/market/",
                NextUrl = "/category/market/page/2/",
                TaxonomyContext = new ListRouteTaxonomyContext
                {
                    Kind = "category",
                    Term = "Market",
                    Slug = "market",
                    IsIndex = false
                }
            },
            new ListRoutePlan
            {
                RouteId = "taxonomy:category:market:2",
                Kind = ListRouteKind.TaxonomyTermPage,
                Url = "/category/market/page/2/",
                OutputPath = "category/market/page/2/index.html",
                Template = "pages/category.html",
                Collection = "insights",
                PageNumber = 2,
                PageSize = 2,
                TotalItems = 3,
                Items = items.Skip(2).ToArray(),
                CanonicalUrl = "/category/market/page/2/",
                PrevUrl = "/category/market/",
                TaxonomyContext = new ListRouteTaxonomyContext
                {
                    Kind = "category",
                    Term = "Market",
                    Slug = "market",
                    IsIndex = false
                }
            },
            new ListRoutePlan
            {
                RouteId = "taxonomy:category:index",
                Kind = ListRouteKind.TaxonomyIndex,
                Url = "/category/",
                OutputPath = "category/index.html",
                Template = "pages/category-index.html",
                PageNumber = 1,
                PageSize = 10,
                TotalItems = 0,
                CanonicalUrl = "/category/",
                TaxonomyContext = new ListRouteTaxonomyContext
                {
                    Kind = "category",
                    IsIndex = true
                }
            },
            new ListRoutePlan
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
                Items = items.Take(1).ToArray(),
                CanonicalUrl = "/companies/malaysia/",
                FilterContext = new ListRouteFilterContext
                {
                    Field = "country",
                    Operator = "equals",
                    Value = "Malaysia"
                }
            }
        });

        var snapshot = ListRouteGraphSnapshot.FromGraph(graph);
        var json = JsonSerializer.Serialize(snapshot, JsonOptions);
        var expectedJson = File.ReadAllText(GetListRouteGraphGoldenPath()).ReplaceLineEndings("\n").TrimEnd('\n');

        Assert.Equal(6, snapshot.Routes.Count);
        Assert.Contains(snapshot.Routes, route => route.Url == "/insights/" && route.Kind == "collectionList" && route.ItemIds.SequenceEqual(new[] { "insight-1", "insight-2" }));
        Assert.Contains(snapshot.Routes, route => route.Url == "/insights/page/2/" && route.Kind == "collectionPage" && route.PrevUrl == "/insights/");
        Assert.Contains(snapshot.Routes, route => route.Url == "/category/market/" && route.TaxonomyContext?.Slug == "market" && route.NextUrl == "/category/market/page/2/");
        Assert.Contains(snapshot.Routes, route => route.Url == "/category/market/page/2/" && route.TaxonomyContext?.Kind == "category" && route.PageNumber == 2);
        Assert.Contains(snapshot.Routes, route => route.Url == "/category/" && route.Kind == "taxonomyIndex" && route.TaxonomyContext?.IsIndex is true);
        var filteredRoute = Assert.Single(snapshot.Routes, route => route.Url == "/companies/malaysia/");
        Assert.Equal("filteredListPage", filteredRoute.Kind);
        Assert.Equal("Malaysia", filteredRoute.FilterContext?.Value);
        Assert.Null(filteredRoute.TaxonomyContext);
        Assert.Equal(expectedJson, json.ReplaceLineEndings("\n"));
    }

    [Fact]
    public void ListRouteItem_FromRoutedContentDocument_CreatesPageInfoCompatibleProjection()
    {
        var routed = Routed("insight-1", "Market Update", "/insights/market-update/", "Market summary");

        var item = ListRouteItem.FromRoutedContentDocument(routed);
        var page = item.ToPageInfo(content: "<p>Market body</p>");

        Assert.Equal("insight-1", item.Id);
        Assert.Equal("Market Update", page.Title);
        Assert.Equal("/insights/market-update/", page.Url);
        Assert.Equal("<p>Market body</p>", page.Content);
        Assert.Equal("Market summary", page.Summary);
        Assert.NotNull(page.Fields);
        Assert.Equal("insights", ContentFieldReader.GetText(page.Fields, "collection"));
        Assert.Equal(routed.Document.Record, page.ContentRecord);
        Assert.Equal(routed.Document.Route, page.Route);
        Assert.Equal(routed.Document.Publish, page.Publish);
    }

    [Fact]
    public void BuildPageFields_FilteredPagedRoute_ExposesPaginationAndFilterContext()
    {
        var route = new ListRoutePlan
        {
            RouteId = "filter:companies:country:malaysia:2",
            Kind = ListRouteKind.FilteredListPage,
            Url = "/companies/malaysia/page/2/",
            OutputPath = "companies/malaysia/page/2/index.html",
            Template = "pages/company-list.html",
            Collection = "companies",
            PageNumber = 2,
            PageSize = 2,
            TotalItems = 3,
            Items = new[] { Item("company-1", "Company 1", "/companies/company-1/", "Company summary") },
            CanonicalUrl = "/companies/malaysia/page/2/",
            PrevUrl = "/companies/malaysia/",
            FilterContext = new ListRouteFilterContext
            {
                Field = "country",
                Value = "Malaysia"
            }
        };

        var fields = ListRouteRenderPlanBuilder.BuildPageFields(route);

        var pagination = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(fields["pagination"].Value);
        Assert.Equal(2, pagination["page"]);
        Assert.Equal(2, pagination["page_size"]);
        Assert.Equal(2, pagination["total_pages"]);
        Assert.Equal(3, pagination["total_items"]);
        Assert.Equal("/companies/malaysia/", pagination["prev_url"]);
        Assert.Null(pagination["next_url"]);

        var filter = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(fields["filter"].Value);
        Assert.Equal("country", filter["field"]);
        Assert.Equal("equals", filter["operator"]);
        Assert.Equal("Malaysia", filter["value"]);

        var items = Assert.IsAssignableFrom<IReadOnlyList<object>>(fields["items"].Value);
        var firstItem = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(items[0]);
        Assert.True(firstItem.ContainsKey("updated_at"));
    }

    [Fact]
    public void AddDerivedTaxonomyRoutes_AddsIndexTermAndPaginationPlans()
    {
        var graph = ListRouteGraph.Create(new[]
        {
            Plan(routeId: "home", url: "/", outputPath: "index.html")
        });
        var derived = new[]
        {
            DerivedTaxonomyIndex(),
            DerivedTaxonomyTerm(page: 1, url: "/insights/category/market/", outputPath: "insights/category/market/index.html", prevUrl: null, nextUrl: "/insights/category/market/page/2/"),
            DerivedTaxonomyTerm(page: 2, url: "/insights/category/market/page/2/", outputPath: "insights/category/market/page/2/index.html", prevUrl: "/insights/category/market/", nextUrl: null)
        };

        var result = ListRouteGraphBuilder.AddDerivedTaxonomyRoutes(graph, derived);

        Assert.Equal(4, result.Routes.Count);
        Assert.Contains(result.Routes, route =>
            route.Kind == ListRouteKind.TaxonomyIndex &&
            route.Url == "/insights/category/" &&
            route.TaxonomyContext?.IsIndex is true &&
            route.Items.Single().Url == "/insights/category/market/");
        Assert.Contains(result.Routes, route =>
            route.Kind == ListRouteKind.TaxonomyTermPage &&
            route.Url == "/insights/category/market/" &&
            route.PageNumber == 1 &&
            route.PageSize == 1 &&
            route.TotalItems == 2 &&
            route.NextUrl == "/insights/category/market/page/2/" &&
            route.Items.Single().Id == "market-2" &&
            route.TaxonomyContext?.Slug == "market");
        Assert.Contains(result.Routes, route =>
            route.Kind == ListRouteKind.TaxonomyTermPage &&
            route.Url == "/insights/category/market/page/2/" &&
            route.PrevUrl == "/insights/category/market/" &&
            route.MetadataRouteUrl == "/insights/category/market/");
    }

    [Fact]
    public void Create_RejectsDuplicateRouteIds()
    {
        var route = Plan(routeId: "collection:insights:1", url: "/insights/", outputPath: "insights/index.html");

        var ex = Assert.Throws<ArgumentException>(() => ListRouteGraph.Create(new[]
        {
            route,
            route with { Url = "/insights/page/2/", OutputPath = "insights/page/2/index.html" }
        }));

        Assert.Contains("Duplicate list route routeId", ex.Message);
    }

    [Fact]
    public void Create_RejectsDuplicateNormalizedUrlsAndOutputPaths()
    {
        var urlConflict = Assert.Throws<ArgumentException>(() => ListRouteGraph.Create(new[]
        {
            Plan(routeId: "collection:insights:1", url: "/insights", outputPath: "insights/index.html"),
            Plan(routeId: "collection:insights:2", url: "/insights/", outputPath: "insights/page/2/index.html")
        }));
        Assert.Contains("Duplicate list route url", urlConflict.Message);

        var outputConflict = Assert.Throws<ArgumentException>(() => ListRouteGraph.Create(new[]
        {
            Plan(routeId: "collection:insights:1", url: "/insights/", outputPath: "insights/index.html"),
            Plan(routeId: "collection:featured:1", url: "/featured/", outputPath: "insights/index.html")
        }));
        Assert.Contains("Duplicate list route outputPath", outputConflict.Message);
    }

    [Fact]
    public void Create_RejectsMissingTemplateCanonicalAndInvalidTotals()
    {
        var missingTemplate = Assert.Throws<ArgumentException>(() => ListRouteGraph.Create(new[]
        {
            Plan(routeId: "collection:insights:1", url: "/insights/", outputPath: "insights/index.html") with { Template = " " }
        }));
        Assert.Contains("template is required", missingTemplate.Message);

        var missingCanonical = Assert.Throws<ArgumentException>(() => ListRouteGraph.Create(new[]
        {
            Plan(routeId: "collection:insights:1", url: "/insights/", outputPath: "insights/index.html") with { CanonicalUrl = " " }
        }));
        Assert.Contains("canonicalUrl is required", missingCanonical.Message);

        var negativeTotal = Assert.Throws<ArgumentException>(() => ListRouteGraph.Create(new[]
        {
            Plan(routeId: "collection:insights:1", url: "/insights/", outputPath: "insights/index.html") with { TotalItems = -1 }
        }));
        Assert.Contains("totalItems must be non-negative", negativeTotal.Message);

        var inconsistentTotal = Assert.Throws<ArgumentException>(() => ListRouteGraph.Create(new[]
        {
            Plan(routeId: "collection:insights:1", url: "/insights/", outputPath: "insights/index.html") with
            {
                TotalItems = 1,
                Items = new[] { Item("a", "A", "/a/", "A"), Item("b", "B", "/b/", "B") }
            }
        }));
        Assert.Contains("totalItems must be greater than or equal to item count", inconsistentTotal.Message);
    }

    [Fact]
    public void FindByRouteId_MatchesCaseInsensitively()
    {
        var graph = ListRouteGraph.Create(new[]
        {
            Plan(routeId: "Collection:Insights:1", url: "/insights/", outputPath: "insights/index.html")
        });

        Assert.NotNull(graph.FindByRouteId("collection:insights:1"));
        Assert.Null(graph.FindByRouteId(" "));
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private static string GetListRouteGraphGoldenPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (directory.EnumerateDirectories("src").Any() && directory.EnumerateDirectories("tests").Any())
            {
                return Path.Combine(directory.FullName, "tests", "Bukit.Engine.Tests", "Snapshots", "ListRouteGraph", "list-route-graph.golden.json");
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Failed to locate repository root for list route graph golden file.");
    }

    private static ListRouteItem Item(string id, string title, string url, string summary)
        => ListRouteItem.FromRoutedContentDocument(Routed(id, title, url, summary));

    private static ListRoutePlan Plan(string routeId, string url, string outputPath)
        => new()
        {
            RouteId = routeId,
            Kind = ListRouteKind.CollectionList,
            Url = url,
            OutputPath = outputPath,
            Template = "pages/list.html",
            TotalItems = 0,
            CanonicalUrl = url.EndsWith('/') ? url : url + "/"
        };

    private static RoutedContentDocument DerivedTaxonomyIndex()
    {
        var fields = ContentFieldReader.ToFieldMap(new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["type"] = "derived",
            ["collection"] = "page",
            ["summary"] = "Browse all category.",
            ["taxonomy"] = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["kind"] = "category",
                ["is_index"] = true
            },
            ["terms"] = new List<object>
            {
                new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                {
                    ["title"] = "Market",
                    ["slug"] = "market",
                    ["url"] = "/insights/category/market/",
                    ["count"] = 2
                }
            }
        });
        var document = ContentDocument.Create(
            "category-index",
            "Categories",
            "category",
            new DateTimeOffset(2026, 7, 7, 0, 0, 0, TimeSpan.Zero),
            null,
            fields);
        var route = new RouteInfo("/insights/category/", "insights/category/index.html", "pages/taxonomy-index.html");
        return new RoutedContentDocument(document, route, document.PublishAt);
    }

    private static RoutedContentDocument DerivedTaxonomyTerm(int page, string url, string outputPath, string? prevUrl, string? nextUrl)
    {
        var fields = new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
        {
            ["type"] = new("text", "derived"),
            ["collection"] = new("text", "page"),
            ["summary"] = new("text", $"Market page {page}"),
            ["taxonomy"] = new("object", new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["kind"] = "category",
                ["term"] = "Market",
                ["slug"] = "market",
                ["count"] = 2
            }),
            ["pagination"] = new("object", new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["page"] = page,
                ["page_size"] = 1,
                ["total"] = 2,
                ["total_pages"] = 2,
                ["has_prev"] = prevUrl is not null,
                ["has_next"] = nextUrl is not null,
                ["prev_url"] = prevUrl,
                ["next_url"] = nextUrl
            }),
            ["items"] = new("list", new List<object>
            {
                new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                {
                    ["id"] = page == 1 ? "market-2" : "market-1",
                    ["title"] = page == 1 ? "Market 2" : "Market 1",
                    ["url"] = page == 1 ? "/insights/market-2/" : "/insights/market-1/",
                    ["summary"] = $"Summary {page}",
                    ["publish_date"] = new DateTime(2026, 7, page, 0, 0, 0, DateTimeKind.Utc),
                    ["fields"] = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["cover"] = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["type"] = "text",
                            ["value"] = $"/covers/market-{page}.jpg"
                        }
                    }
                }
            })
        };
        var document = ContentDocument.Create(
            page == 1 ? "category-market" : "category-market-page-2",
            page == 1 ? "Category: Market" : "Category: Market (Page 2)",
            "market",
            new DateTimeOffset(2026, 7, page, 0, 0, 0, TimeSpan.Zero),
            null,
            fields);
        return new RoutedContentDocument(document, new RouteInfo(url, outputPath, "pages/taxonomy-term.html"), document.PublishAt);
    }

    private static RoutedContentDocument Routed(string id, string title, string url, string summary)
    {
        var fields = ContentFieldReader.ToFieldMap(new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["collection"] = "insights",
            ["summary"] = summary
        });
        var document = ContentDocument.Create(
            id,
            title,
            id,
            new DateTimeOffset(2026, 7, 7, 0, 0, 0, TimeSpan.Zero),
            $"<p>{title}</p>",
            fields);

        return new RoutedContentDocument(
            document,
            new RouteInfo(url, url.Trim('/').Replace('/', Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar + "index.html", "pages/insight.html"),
            document.PublishAt);
    }
}
