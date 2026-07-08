using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Rendering;
using Bukit.Routing;
using Bukit.Engine.Abstractions.Routing;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class SeoIndexBuilderTests
{
    private static AppConfig CreateConfig(bool seoEnabled = true)
    {
        return new AppConfig
        {
            Content = TestContent.Markdown(),
            Site = new SiteConfig
            {
                Name = "test",
                Title = "Test Site",
                Url = "https://example.com",
                Language = "zh-CN",
                Seo = new SeoConfig
                {
                    Enabled = seoEnabled,
                    Schema = new SeoSchemaConfig
                    {
                        SearchAction = true,
                        WebPage = true,
                        CollectionPage = false
                    }
                }
            }
        };
    }

    [Fact]
    public void Build_SeoDisabled_ReturnsEmpty()
    {
        var config = CreateConfig(seoEnabled: false);
        var routed = new (ContentDocument, RouteInfo)[]
        {
            (ContentDocument.Create(
                id: "p1",
                title: "Page",
                slug: "page",
                publishAt: DateTimeOffset.UtcNow,
                contentHtml: null,
                fields: ContentFieldReader.ToFieldMap(new Dictionary<string, object> { ["type"] = "page" })),
             new RouteInfo("/pages/page/", "pages/page/index.html", "pages/page.html"))
        };

        var result = SeoIndexBuilder.Build(config, "/", routed.ToRoutedDocuments(), Array.Empty<RouteInfo>(), new Dictionary<string, IReadOnlyList<SeoAlternateModel>>());

        Assert.Empty(result.Entries);
        Assert.Empty(result.Models);
    }

    [Fact]
    public void Build_WithRoutedItems_CreatesEntriesAndModels()
    {
        var config = CreateConfig();
        var publishAt = new DateTimeOffset(2025, 6, 1, 10, 0, 0, TimeSpan.Zero);
        var routed = new (ContentDocument, RouteInfo)[]
        {
            (ContentDocument.Create(
                id: "post-1",
                title: "First Post",
                slug: "first-post",
                publishAt: publishAt,
                contentHtml: null,
                fields: ContentFieldReader.ToFieldMap(new Dictionary<string, object>
                {
                    ["type"] = "post",
                    ["collection"] = "post",
                    ["summary"] = "First summary"
                })),
             new RouteInfo("/blog/first-post/", "blog/first-post/index.html", "pages/post.html")),
            (ContentDocument.Create(
                id: "page-1",
                title: "About",
                slug: "about",
                publishAt: publishAt,
                contentHtml: null,
                fields: ContentFieldReader.ToFieldMap(new Dictionary<string, object>
                {
                    ["type"] = "page",
                    ["summary"] = "About us"
                })),
             new RouteInfo("/pages/about/", "pages/about/index.html", "pages/page.html"))
        };

        var result = SeoIndexBuilder.Build(config, "/", routed.ToRoutedDocuments(), Array.Empty<RouteInfo>(), new Dictionary<string, IReadOnlyList<SeoAlternateModel>>());

        Assert.Equal(2, result.Entries.Count);
        Assert.Equal(2, result.Models.Count);

        Assert.True(result.Entries.ContainsKey("blog/first-post/index.html"));
        var postEntry = result.Entries["blog/first-post/index.html"];
        Assert.True(postEntry.Indexable);
        Assert.Equal("https://example.com/blog/first-post/", postEntry.Canonical);
        Assert.Equal("post-1", postEntry.SourceItemId);
        Assert.Equal("post", postEntry.ContentType);
        Assert.False(postEntry.IsDerived);

        Assert.True(result.Entries.ContainsKey("pages/about/index.html"));
        var pageEntry = result.Entries["pages/about/index.html"];
        Assert.True(pageEntry.Indexable);
        Assert.Equal("page", pageEntry.ContentType);

        Assert.True(result.Models.ContainsKey("blog/first-post/index.html"));
        Assert.True(result.Models.ContainsKey("pages/about/index.html"));
    }

    [Fact]
    public void Build_WithListRoutes_CreatesListEntries()
    {
        var config = CreateConfig();
        var routed = new (ContentDocument, RouteInfo)[]
        {
            (ContentDocument.Create(
                id: "post-1",
                title: "Post",
                slug: "post",
                publishAt: DateTimeOffset.UtcNow,
                contentHtml: null,
                fields: ContentFieldReader.ToFieldMap(new Dictionary<string, object> { ["type"] = "post", ["collection"] = "post" })),
             new RouteInfo("/blog/post/", "blog/post/index.html", "pages/post.html"))
        };
        var listRoutes = new[]
        {
            new RouteInfo("/", "index.html", "pages/index.html"),
            new RouteInfo("/blog/", "blog/index.html", "pages/list.html")
        };

        var result = SeoIndexBuilder.Build(config, "/", routed.ToRoutedDocuments(), listRoutes, new Dictionary<string, IReadOnlyList<SeoAlternateModel>>());

        Assert.True(result.Entries.ContainsKey("index.html"));
        Assert.True(result.Entries.ContainsKey("blog/index.html"));
        Assert.True(result.Entries.ContainsKey("blog/post/index.html"));

        var homeEntry = result.Entries["index.html"];
        Assert.Equal("list", homeEntry.ContentType);
        Assert.Null(homeEntry.SourceItemId);
        Assert.True(homeEntry.IsDerived);

        var blogEntry = result.Entries["blog/index.html"];
        Assert.Equal("list", blogEntry.ContentType);
        Assert.True(blogEntry.IsDerived);
    }

    [Fact]
    public void Build_WithListRouteGraph_UsesTaxonomyGraphRouteForSeoEntry()
    {
        var config = CreateConfig();
        var derivedDocument = ContentDocument.Create(
            id: "category-market-page-2",
            title: "Category: Market (Page 2)",
            slug: "market",
            publishAt: new DateTimeOffset(2026, 7, 2, 0, 0, 0, TimeSpan.Zero),
            contentHtml: null,
            fields: ContentFieldReader.ToFieldMap(new Dictionary<string, object>
            {
                ["type"] = "derived",
                ["collection"] = "page",
                ["summary"] = "Browse 1 content items in Market. Page 2 of 2."
            }));
        var route = new RouteInfo("/insights/category/market/page/2/", "insights/category/market/page/2/index.html", "pages/taxonomy-term.html");
        var routed = new[] { (derivedDocument, route) };
        var graph = ListRouteGraph.Create(new[]
        {
            new ListRoutePlan
            {
                RouteId = "taxonomy:category:market:2",
                Kind = ListRouteKind.TaxonomyTermPage,
                Url = route.Url,
                OutputPath = route.OutputPath,
                Template = route.Template,
                PageNumber = 2,
                PageSize = 1,
                TotalItems = 2,
                Items = new[]
                {
                    new ListRouteItem
                    {
                        Id = "market-1",
                        Title = "Market 1",
                        Url = "/insights/market-1/",
                        Summary = "Summary 1",
                        PublishDate = new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero)
                    }
                },
                CanonicalUrl = route.Url,
                PrevUrl = "/insights/category/market/",
                TaxonomyContext = new ListRouteTaxonomyContext
                {
                    Kind = "category",
                    Term = "Market",
                    Slug = "market"
                }
            }
        });
        var alternates = new Dictionary<string, IReadOnlyList<SeoAlternateModel>>(StringComparer.OrdinalIgnoreCase)
        {
            [$"route:{route.Url}"] = new[] { new SeoAlternateModel("en", "https://example.com/insights/category/market/page/2/") }
        };

        var result = SeoIndexBuilder.Build(config, "/", routed.ToRoutedDocuments(), Array.Empty<RouteInfo>(), alternates, graph);

        Assert.Single(result.Entries);
        var entry = result.Entries["insights/category/market/page/2/index.html"];
        Assert.Equal("taxonomy", entry.ContentType);
        Assert.True(entry.IsDerived);
        Assert.Null(entry.SourceItemId);
        Assert.Equal("https://example.com/insights/category/market/page/2/", entry.Canonical);

        var model = result.Models["insights/category/market/page/2/index.html"];
        Assert.Equal("Category: Market (Page 2)", model.Title);
        Assert.Equal("Browse 1 content items in Market. Page 2 of 2.", model.Description);
        Assert.Single(model.Alternates);
    }

    [Fact]
    public void Build_WithListRouteGraph_IncludesFilteredPaginationPage()
    {
        var config = CreateConfig();
        var route = new ListRoutePlan
        {
            RouteId = "filter:companies:country:Malaysia:2",
            Kind = ListRouteKind.FilteredListPage,
            Url = "/companies/malaysia/page/2/",
            OutputPath = "companies/malaysia/page/2/index.html",
            Template = "pages/company-list.html",
            Collection = "companies",
            PageNumber = 2,
            PageSize = 2,
            TotalItems = 3,
            Items = new[]
            {
                new ListRouteItem
                {
                    Id = "company-1",
                    Title = "Company 1",
                    Url = "/companies/company-1/",
                    Summary = "Company summary",
                    PublishDate = new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero)
                }
            },
            CanonicalUrl = "/companies/malaysia/page/2/",
            PrevUrl = "/companies/malaysia/",
            FilterContext = new ListRouteFilterContext
            {
                Field = "country",
                Value = "Malaysia"
            }
        };
        var graph = ListRouteGraph.Create(new[] { route });

        var result = SeoIndexBuilder.Build(
            config,
            "/",
            Array.Empty<RoutedContentDocument>(),
            Array.Empty<RouteInfo>(),
            new Dictionary<string, IReadOnlyList<SeoAlternateModel>>(),
            graph);

        Assert.Single(result.Entries);
        var entry = result.Entries["companies/malaysia/page/2/index.html"];
        Assert.Equal("list", entry.ContentType);
        Assert.True(entry.IsDerived);
        Assert.Null(entry.SourceItemId);
        Assert.Equal("https://example.com/companies/malaysia/page/2/", entry.Canonical);
        Assert.True(entry.Indexable);

        Assert.True(result.Models.ContainsKey("companies/malaysia/page/2/index.html"));
        var model = result.Models["companies/malaysia/page/2/index.html"];
        Assert.Equal("https://example.com/companies/malaysia/page/2/", model.Canonical);
        Assert.Equal("https://example.com/companies/malaysia/", model.Prev);
        Assert.Null(model.Next);
    }

    [Fact]
    public void Build_WithListRouteGraph_UsesGraphCanonicalPrevAndNext()
    {
        var config = CreateConfig();
        var route = new ListRoutePlan
        {
            RouteId = "collection:insights:2",
            Kind = ListRouteKind.CollectionPage,
            Url = "/insights/page-two/",
            OutputPath = "insights/page-two/index.html",
            Template = "pages/insight-list.html",
            Collection = "insight",
            PageNumber = 2,
            PageSize = 10,
            TotalItems = 30,
            CanonicalUrl = "/insights/p/2/",
            PrevUrl = "/insights/",
            NextUrl = "/insights/p/3/"
        };
        var graph = ListRouteGraph.Create(new[] { route });

        var result = SeoIndexBuilder.Build(
            config,
            "/",
            Array.Empty<RoutedContentDocument>(),
            Array.Empty<RouteInfo>(),
            new Dictionary<string, IReadOnlyList<SeoAlternateModel>>(),
            graph);

        var entry = result.Entries["insights/page-two/index.html"];
        Assert.Equal("https://example.com/insights/p/2/", entry.Canonical);

        var model = result.Models["insights/page-two/index.html"];
        Assert.Equal("https://example.com/insights/p/2/", model.Canonical);
        Assert.Equal("https://example.com/insights/", model.Prev);
        Assert.Equal("https://example.com/insights/p/3/", model.Next);
    }

    [Fact]
    public void Build_DerivedDocuments_AreMarkedExplicitly()
    {
        var config = CreateConfig();
        var routed = new (ContentDocument, RouteInfo)[]
        {
            (ContentDocument.Create(
                id: "tags-index",
                title: "Tags",
                slug: "tags",
                publishAt: DateTimeOffset.UtcNow,
                contentHtml: null,
                fields: ContentFieldReader.ToFieldMap(new Dictionary<string, object>
                {
                    ["type"] = "derived",
                    ["collection"] = "taxonomy"
                })),
             new RouteInfo("/tags/", "tags/index.html", "pages/taxonomy-index.html"))
        };

        var result = SeoIndexBuilder.Build(config, "/", routed.ToRoutedDocuments(), Array.Empty<RouteInfo>(), new Dictionary<string, IReadOnlyList<SeoAlternateModel>>());

        var entry = result.Entries["tags/index.html"];
        Assert.True(entry.IsDerived);
        Assert.Equal("taxonomy", entry.ContentType);
    }

    [Fact]
    public void Build_PrefersCanonicalCollectionFromFields()
    {
        var config = CreateConfig();
        var routed = new (ContentDocument, RouteInfo)[]
        {
            (ContentDocument.Create(
                id: "post-1",
                title: "Post",
                slug: "post",
                publishAt: DateTimeOffset.UtcNow,
                contentHtml: null,
            fields: new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
                {
                    ["type"] = new("text", "post"),
                    ["collection"] = new("text", "knowledge")
                }),
             new RouteInfo("/knowledge/post/", "knowledge/post/index.html", "pages/post.html"))
        };

        var result = SeoIndexBuilder.Build(config, "/", routed.ToRoutedDocuments(), Array.Empty<RouteInfo>(), new Dictionary<string, IReadOnlyList<SeoAlternateModel>>());

        var entry = result.Entries["knowledge/post/index.html"];
        Assert.Equal("knowledge", entry.ContentType);
    }

    [Fact]
    public void Build_EntryHasLastModified()
    {
        var config = CreateConfig();
        var publishAt = new DateTimeOffset(2025, 3, 15, 0, 0, 0, TimeSpan.Zero);
        var routed = new (ContentDocument, RouteInfo)[]
        {
            (ContentDocument.Create(
                id: "post-1",
                title: "Post",
                slug: "post",
                publishAt: publishAt,
                contentHtml: null,
                fields: ContentFieldReader.ToFieldMap(new Dictionary<string, object> { ["type"] = "post", ["collection"] = "post" })),
             new RouteInfo("/blog/post/", "blog/post/index.html", "pages/post.html"))
        };

        var result = SeoIndexBuilder.Build(config, "/", routed.ToRoutedDocuments(), Array.Empty<RouteInfo>(), new Dictionary<string, IReadOnlyList<SeoAlternateModel>>());

        var entry = result.Entries["blog/post/index.html"];
        Assert.True(entry.LastModified > DateTimeOffset.MinValue);
    }

    [Fact]
    public void Build_NonIndexableContent()
    {
        var config = CreateConfig();
        var routed = new (ContentDocument, RouteInfo)[]
        {
            (ContentDocument.Create(
                id: "p1",
                title: "Hidden",
                slug: "hidden",
                publishAt: DateTimeOffset.UtcNow,
                contentHtml: null,
                fields: ContentFieldReader.ToFieldMap(new Dictionary<string, object>
                {
                    ["type"] = "page",
                    ["robots"] = "noindex"
                })),
             new RouteInfo("/pages/hidden/", "pages/hidden/index.html", "pages/page.html"))
        };

        var result = SeoIndexBuilder.Build(config, "/", routed.ToRoutedDocuments(), Array.Empty<RouteInfo>(), new Dictionary<string, IReadOnlyList<SeoAlternateModel>>());

        var entry = result.Entries["pages/hidden/index.html"];
        Assert.False(entry.Indexable);
        Assert.Equal("noindex", entry.Robots);
    }

    [Fact]
    public void Build_WithAlternates_PassesToModels()
    {
        var config = CreateConfig();
        var item = ContentDocument.Create(
            id: "p1",
            title: "Translated",
            slug: "translated",
            publishAt: DateTimeOffset.UtcNow,
            contentHtml: null,
            fields: ContentFieldReader.ToFieldMap(new Dictionary<string, object>
            {
                ["type"] = "page",
                ["i18n_key"] = "page.translated"
            }));
        var route = new RouteInfo("/pages/translated/", "pages/translated/index.html", "pages/page.html");
        var routed = new (ContentDocument, RouteInfo)[] { (item, route) };
        var alternates = new Dictionary<string, IReadOnlyList<SeoAlternateModel>>(StringComparer.OrdinalIgnoreCase)
        {
            ["i18n:page.translated"] = new[]
            {
                new SeoAlternateModel("en", "https://example.com/pages/translated/"),
                new SeoAlternateModel("ja", "https://example.com/ja/pages/translated/")
            }
        };

        var result = SeoIndexBuilder.Build(config, "/", routed.ToRoutedDocuments(), Array.Empty<RouteInfo>(), alternates);

        var model = result.Models["pages/translated/index.html"];
        Assert.Equal(2, model.Alternates.Count);
    }

    [Fact]
    public void Build_BaseUrlIsPrependedToCanonical()
    {
        var config = CreateConfig();
        var routed = new (ContentDocument, RouteInfo)[]
        {
            (ContentDocument.Create(
                id: "p1",
                title: "Page",
                slug: "page",
                publishAt: DateTimeOffset.UtcNow,
                contentHtml: null,
                fields: ContentFieldReader.ToFieldMap(new Dictionary<string, object> { ["type"] = "page" })),
             new RouteInfo("/pages/page/", "pages/page/index.html", "pages/page.html"))
        };

        var result = SeoIndexBuilder.Build(config, "/zh", routed.ToRoutedDocuments(), Array.Empty<RouteInfo>(), new Dictionary<string, IReadOnlyList<SeoAlternateModel>>());

        var entry = result.Entries["pages/page/index.html"];
        Assert.Equal("https://example.com/zh/pages/page/", entry.Canonical);
    }

    [Fact]
    public void Build_ListRoutesWithoutRouted_FieldsNull()
    {
        var config = CreateConfig();
        var listRoutes = new[]
        {
            new RouteInfo("/blog/", "blog/index.html", "pages/list.html")
        };

        var result = SeoIndexBuilder.Build(config, "/", Array.Empty<RoutedContentDocument>(), listRoutes, new Dictionary<string, IReadOnlyList<SeoAlternateModel>>());

        Assert.True(result.Entries.ContainsKey("blog/index.html"));
        Assert.Equal("list", result.Entries["blog/index.html"].ContentType);
    }
}
