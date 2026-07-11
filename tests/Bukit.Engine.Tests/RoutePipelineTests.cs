using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Routing;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Shared;
using Bukit.Theme;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class RoutePipelineTests
{
    [Fact]
    public void Execute_GeneratesContentRoutesWithCollectionRulesAndListRoutes()
    {
        var post = Item("hello", "hello", new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["collection"] = "article",
            ["featured"] = "true"
        });
        var regularPost = Item("plain", "plain", new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["collection"] = "article",
            ["featured"] = "false"
        });
        var page = Item("about", "about", new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["type"] = "page",
            ["collection"] = "page",
            ["featured"] = "true"
        });
        var data = Item("settings", "settings", new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["sourceMode"] = "data"
        });
        var pipeline = new RoutePipeline();

        var result = pipeline.Execute(Config(), new[] { post, regularPost, page, data }, TemplateResolver());

        Assert.Equal(new[] { post.Id, regularPost.Id, page.Id }, result.ContentDocuments.Select(x => x.Id));
        Assert.Equal(3, result.RoutedDocuments.Count);
        Assert.Equal(post.Id, result.RoutedDocuments[0].Document.Id);
        Assert.Equal("/articles/hello/", result.RoutedDocuments[0].Route.Url);
        Assert.Equal("articles/hello/index.html", Normalize(result.RoutedDocuments[0].Route.OutputPath));
        Assert.Equal("pages/article.html", result.RoutedDocuments[0].Route.Template);
        Assert.Equal(regularPost.Id, result.RoutedDocuments[1].Document.Id);
        Assert.Equal("/articles/plain/", result.RoutedDocuments[1].Route.Url);
        Assert.Equal(page.Id, result.RoutedDocuments[2].Document.Id);
        Assert.Equal("/pages/about/", result.RoutedDocuments[2].Route.Url);
        Assert.Equal(result.ListRouteGraph.Routes.Select(route => route.Url), result.ListRoutes.Select(route => route.Url));
        Assert.Equal(result.ListRouteGraph.Routes.Select(route => Normalize(route.OutputPath)), result.ListRoutes.Select(route => Normalize(route.OutputPath)));
        Assert.Contains(result.ListRoutes, route => route.Url == "/" && route.OutputPath == "index.html");
        Assert.Contains(result.ListRoutes, route => route.Url == "/articles/" && Normalize(route.OutputPath) == "articles/index.html" && route.Template == "pages/article-list.html");
        Assert.Contains(result.ListRoutes, route => route.Url == "/articles/featured/" && Normalize(route.OutputPath) == "articles/featured/index.html" && route.Template == "pages/featured.html");

        var home = Assert.Single(result.ListRouteGraph.Routes, route => route.RouteId == "home");
        Assert.Equal(ListRouteKind.Home, home.Kind);
        Assert.Equal(new[] { post.Id, regularPost.Id, page.Id }, home.Items.Select(item => item.Id));
        Assert.DoesNotContain(home.Items, item => item.Id == data.Id);

        var articleList = Assert.Single(result.ListRouteGraph.Routes, route => route.RouteId == "collection:article:1");
        Assert.Equal(ListRouteKind.CollectionList, articleList.Kind);
        Assert.Equal("article", articleList.Collection);
        Assert.Equal(new[] { post.Id, regularPost.Id }, articleList.Items.Select(item => item.Id));

        var filtered = Assert.Single(result.ListRouteGraph.Routes, route => route.RouteId == "filter:article:featured:true:1");
        Assert.Equal(ListRouteKind.FilteredListPage, filtered.Kind);
        Assert.Equal("/articles/featured/", filtered.Url);
        Assert.Equal("featured", filtered.FilterContext?.Field);
        Assert.Equal("equals", filtered.FilterContext?.Operator);
        Assert.Equal("true", filtered.FilterContext?.Value);
        Assert.Equal(new[] { post.Id }, filtered.Items.Select(item => item.Id));
        Assert.DoesNotContain(filtered.Items, item => item.Id == regularPost.Id);
        Assert.DoesNotContain(filtered.Items, item => item.Id == page.Id);
        Assert.DoesNotContain(filtered.Items, item => item.Id == data.Id);
    }

    [Fact]
    public void Execute_WhenFilteredListPaginationConfigured_GeneratesPagedFilterRoutes()
    {
        var companies = Enumerable.Range(1, 5)
            .Select(i => Item($"company-{i}", $"company-{i}", new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["collection"] = "company",
                ["country"] = i % 2 == 1 ? "Malaysia" : "Singapore"
            }, new DateTimeOffset(2026, 1, i, 0, 0, 0, TimeSpan.Zero)))
            .ToArray();
        var pipeline = new RoutePipeline();

        var result = pipeline.Execute(FilteredPaginatedConfig(), companies, TemplateResolver());

        var first = Assert.Single(result.ListRouteGraph.Routes, route => route.RouteId == "filter:company:country:Malaysia:1");
        Assert.Equal(ListRouteKind.FilteredListPage, first.Kind);
        Assert.Equal("/companies/malaysia/", first.Url);
        Assert.Equal("companies/malaysia/index.html", Normalize(first.OutputPath));
        Assert.Equal("pages/company-filter.html", first.Template);
        Assert.Equal(1, first.PageNumber);
        Assert.Equal(2, first.PageSize);
        Assert.Equal(3, first.TotalItems);
        Assert.Null(first.PrevUrl);
        Assert.Equal("/companies/malaysia/page/2/", first.NextUrl);
        Assert.Equal("country", first.FilterContext?.Field);
        Assert.Equal("equals", first.FilterContext?.Operator);
        Assert.Equal("Malaysia", first.FilterContext?.Value);
        Assert.Equal(new[] { "company-5", "company-3" }, first.Items.Select(item => item.Id));

        var second = Assert.Single(result.ListRouteGraph.Routes, route => route.RouteId == "filter:company:country:Malaysia:2");
        Assert.Equal(ListRouteKind.FilteredListPage, second.Kind);
        Assert.Equal("/companies/malaysia/page/2/", second.Url);
        Assert.Equal("companies/malaysia/page/2/index.html", Normalize(second.OutputPath));
        Assert.Equal("pages/company-filter.html", second.Template);
        Assert.Equal(2, second.PageNumber);
        Assert.Equal(2, second.PageSize);
        Assert.Equal(3, second.TotalItems);
        Assert.Equal("/companies/malaysia/", second.PrevUrl);
        Assert.Null(second.NextUrl);
        Assert.Equal("Malaysia", second.FilterContext?.Value);
        Assert.Equal(new[] { "company-1" }, second.Items.Select(item => item.Id));

        Assert.Equal(
            new[] { "/", "/companies/", "/companies/malaysia/", "/companies/malaysia/page/2/" },
            result.ListRoutes.Select(route => route.Url));
    }

    [Fact]
    public void Execute_WhenFilteredListEmptyBehaviorSkip_Configured_SkipsEmptyFilterRoute()
    {
        var companies = Enumerable.Range(1, 2)
            .Select(i => Item($"company-{i}", $"company-{i}", new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["collection"] = "company",
                ["country"] = "Singapore"
            }, new DateTimeOffset(2026, 1, i, 0, 0, 0, TimeSpan.Zero)))
            .ToArray();
        var pipeline = new RoutePipeline();

        var result = pipeline.Execute(FilteredPaginatedConfig(emptyBehavior: "skip"), companies, TemplateResolver());

        Assert.DoesNotContain(result.ListRouteGraph.Routes, route => route.Kind == ListRouteKind.FilteredListPage);
        Assert.Equal(new[] { "/", "/companies/" }, result.ListRoutes.Select(route => route.Url));
    }

    [Fact]
    public void Execute_WhenFilteredListInOperatorConfigured_MatchesAnyListValue()
    {
        var companies = new[]
        {
            Item("company-1", "company-1", new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["collection"] = "company",
                ["category"] = new[] { "市场观察", "公司动态" }
            }, new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)),
            Item("company-2", "company-2", new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["collection"] = "company",
                ["category"] = new[] { "企业公告" }
            }, new DateTimeOffset(2026, 1, 2, 0, 0, 0, TimeSpan.Zero)),
            Item("company-3", "company-3", new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["collection"] = "company",
                ["category"] = new[] { "政策动态" }
            }, new DateTimeOffset(2026, 1, 3, 0, 0, 0, TimeSpan.Zero))
        };
        var pipeline = new RoutePipeline();

        var result = pipeline.Execute(FilteredOperatorConfig(), companies, TemplateResolver());

        var route = Assert.Single(result.ListRouteGraph.Routes, item => item.Url == "/companies/market/");
        Assert.Equal(ListRouteKind.FilteredListPage, route.Kind);
        Assert.Equal("in", route.FilterContext?.Operator);
        Assert.Equal("市场观察", route.FilterContext?.Value);
        Assert.Equal(new[] { "市场观察", "政策动态" }, route.FilterContext?.Values);
        Assert.Equal(new[] { "company-3", "company-1" }, route.Items.Select(item => item.Id));
    }

    [Fact]
    public void Execute_WhenFilteredListContainsOperatorConfigured_MatchesTextAndListValues()
    {
        var companies = new[]
        {
            Item("company-1", "company-1", new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["collection"] = "company",
                ["industry"] = "Malaysia Logistics"
            }, new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)),
            Item("company-2", "company-2", new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["collection"] = "company",
                ["industry"] = new[] { "Manufacturing", "Regional Logistics" }
            }, new DateTimeOffset(2026, 1, 2, 0, 0, 0, TimeSpan.Zero)),
            Item("company-3", "company-3", new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["collection"] = "company",
                ["industry"] = "Healthcare"
            }, new DateTimeOffset(2026, 1, 3, 0, 0, 0, TimeSpan.Zero))
        };
        var pipeline = new RoutePipeline();

        var result = pipeline.Execute(FilteredContainsConfig(), companies, TemplateResolver());

        var route = Assert.Single(result.ListRouteGraph.Routes, item => item.Url == "/companies/logistics/");
        Assert.Equal(ListRouteKind.FilteredListPage, route.Kind);
        Assert.Equal("contains", route.FilterContext?.Operator);
        Assert.Equal("logistics", route.FilterContext?.Value);
        Assert.Equal(new[] { "logistics" }, route.FilterContext?.Values);
        Assert.Equal(new[] { "company-2", "company-1" }, route.Items.Select(item => item.Id));
    }

    [Fact]
    public void Execute_WhenFilteredListPaginationOmitted_UsesCollectionPaginationConfig()
    {
        var companies = Enumerable.Range(1, 3)
            .Select(i => Item($"company-{i}", $"company-{i}", new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["collection"] = "company",
                ["country"] = "Malaysia"
            }, new DateTimeOffset(2026, 1, i, 0, 0, 0, TimeSpan.Zero)))
            .ToArray();
        var pipeline = new RoutePipeline();

        var result = pipeline.Execute(FilteredUsesCollectionPaginationConfig(), companies, TemplateResolver());

        var first = Assert.Single(result.ListRouteGraph.Routes, route => route.RouteId == "filter:company:country:Malaysia:1");
        Assert.Equal("/companies/malaysia/", first.Url);
        Assert.Equal(2, first.PageSize);
        Assert.Equal(3, first.TotalItems);
        Assert.Equal("/companies/malaysia/p/2/", first.NextUrl);
        Assert.Equal(new[] { "company-3", "company-2" }, first.Items.Select(item => item.Id));

        var second = Assert.Single(result.ListRouteGraph.Routes, route => route.RouteId == "filter:company:country:Malaysia:2");
        Assert.Equal("/companies/malaysia/p/2/", second.Url);
        Assert.Equal("/companies/malaysia/", second.PrevUrl);
        Assert.Null(second.NextUrl);
        Assert.Equal(new[] { "company-1" }, second.Items.Select(item => item.Id));
    }

    [Fact]
    public void Execute_WhenContentRoutesConflict_ThrowsConfigException()
    {
        var first = Item("first", "same", new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["type"] = "post",
            ["collection"] = "post"
        });
        var second = Item("second", "same", new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["type"] = "post",
            ["collection"] = "post"
        });
        var pipeline = new RoutePipeline();

        var ex = Assert.Throws<ConfigException>(() => pipeline.Execute(new AppConfig
        {
            Site = new SiteConfig
            {
                Name = "test",
                Title = "Test",
                Collections = new Dictionary<string, CollectionConfig>(StringComparer.OrdinalIgnoreCase)
                {
                    ["post"] = new CollectionConfig
                    {
                        Permalink = "/blog/{slug}/",
                        Template = "content/post.html"
                    }
                }
            },
            Content = TestContent.Markdown()
        }, new[] { first, second }));

        Assert.Contains("Route conflict", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Execute_WhenListRoutesConflict_ThrowsConfigException()
    {
        var post = Item("first", "first", new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["collection"] = "post"
        });
        var pipeline = new RoutePipeline();

        var ex = Assert.Throws<ConfigException>(() => pipeline.Execute(new AppConfig
        {
            Site = new SiteConfig
            {
                Name = "test",
                Title = "Test",
                Collections = new Dictionary<string, CollectionConfig>(StringComparer.OrdinalIgnoreCase)
                {
                    ["post"] = new CollectionConfig
                    {
                        Permalink = "/blog/{slug}/",
                        Template = "content/post.html",
                        ListRoute = "/",
                        ListTemplate = "indexes/list.html"
                    }
                }
            },
            Content = TestContent.Markdown()
        }, new[] { post }));

        Assert.Equal(DiagnosticCode.ConfigInvalidValue, ex.Code);
        Assert.Contains("Invalid list route configuration", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Execute_WhenCollectionPaginationEnabled_SlicesCollectionListRoutes()
    {
        var posts = Enumerable.Range(1, 5)
            .Select(i => Item($"post-{i}", $"post-{i}", new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["collection"] = "article"
            }, new DateTimeOffset(2026, 1, i, 0, 0, 0, TimeSpan.Zero)))
            .ToArray();
        var pipeline = new RoutePipeline();

        var result = pipeline.Execute(PaginatedConfig(), posts, TemplateResolver());

        var first = Assert.Single(result.ListRouteGraph.Routes, route => route.RouteId == "collection:article:1");
        Assert.Equal(ListRouteKind.CollectionList, first.Kind);
        Assert.Equal("/articles/", first.Url);
        Assert.Equal(2, first.PageSize);
        Assert.Equal(5, first.TotalItems);
        Assert.Equal("/articles/page/2/", first.NextUrl);
        Assert.Equal(new[] { "post-5", "post-4" }, first.Items.Select(item => item.Id));

        var second = Assert.Single(result.ListRouteGraph.Routes, route => route.RouteId == "collection:article:2");
        Assert.Equal(ListRouteKind.CollectionPage, second.Kind);
        Assert.Equal("/articles/page/2/", second.Url);
        Assert.Equal("/articles/", second.PrevUrl);
        Assert.Equal("/articles/page/3/", second.NextUrl);
        Assert.Equal(new[] { "post-3", "post-2" }, second.Items.Select(item => item.Id));

        var third = Assert.Single(result.ListRouteGraph.Routes, route => route.RouteId == "collection:article:3");
        Assert.Equal(ListRouteKind.CollectionPage, third.Kind);
        Assert.Equal("/articles/page/3/", third.Url);
        Assert.Equal("/articles/page/2/", third.PrevUrl);
        Assert.Null(third.NextUrl);
        Assert.Equal(new[] { "post-1" }, third.Items.Select(item => item.Id));

        Assert.Equal(
            new[] { "/", "/articles/", "/articles/page/2/", "/articles/page/3/" },
            result.ListRoutes.Select(route => route.Url));
    }

    [Fact]
    public void Execute_WhenPaginationPatternConfigured_UsesPatternAndFirstPagePolicy()
    {
        var posts = Enumerable.Range(1, 3)
            .Select(i => Item($"post-{i}", $"post-{i}", new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["collection"] = "article"
            }, new DateTimeOffset(2026, 1, i, 0, 0, 0, TimeSpan.Zero)))
            .ToArray();
        var pipeline = new RoutePipeline();

        var result = pipeline.Execute(PaginatedConfig(urlPattern: "p/{page}/", firstPageUsesListRoute: false), posts, TemplateResolver());

        var first = Assert.Single(result.ListRouteGraph.Routes, route => route.RouteId == "collection:article:1");
        Assert.Equal("/articles/p/1/", first.Url);
        Assert.Null(first.PrevUrl);
        Assert.Equal("/articles/p/2/", first.NextUrl);
        Assert.Equal(new[] { "post-3", "post-2" }, first.Items.Select(item => item.Id));

        var second = Assert.Single(result.ListRouteGraph.Routes, route => route.RouteId == "collection:article:2");
        Assert.Equal("/articles/p/2/", second.Url);
        Assert.Equal("/articles/p/1/", second.PrevUrl);
        Assert.Null(second.NextUrl);
        Assert.Equal(new[] { "post-1" }, second.Items.Select(item => item.Id));

        Assert.DoesNotContain(result.ListRoutes, route => route.Url == "/articles/");
        Assert.Equal(
            new[] { "/", "/articles/p/1/", "/articles/p/2/" },
            result.ListRoutes.Select(route => route.Url));
    }

    [Fact]
    public void Execute_WhenPaginationPatternConfiguredAndFirstPageUsesListRoute_KeepsListRouteForPageOne()
    {
        var posts = Enumerable.Range(1, 3)
            .Select(i => Item($"post-{i}", $"post-{i}", new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["collection"] = "article"
            }, new DateTimeOffset(2026, 1, i, 0, 0, 0, TimeSpan.Zero)))
            .ToArray();
        var pipeline = new RoutePipeline();

        var result = pipeline.Execute(PaginatedConfig(urlPattern: "p/{num}", firstPageUsesListRoute: true), posts, TemplateResolver());

        var first = Assert.Single(result.ListRouteGraph.Routes, route => route.RouteId == "collection:article:1");
        Assert.Equal("/articles/", first.Url);
        Assert.Null(first.PrevUrl);
        Assert.Equal("/articles/p/2/", first.NextUrl);

        var second = Assert.Single(result.ListRouteGraph.Routes, route => route.RouteId == "collection:article:2");
        Assert.Equal("/articles/p/2/", second.Url);
        Assert.Equal("/articles/", second.PrevUrl);
        Assert.Null(second.NextUrl);

        Assert.Equal(
            new[] { "/", "/articles/", "/articles/p/2/" },
            result.ListRoutes.Select(route => route.Url));
    }

    [Fact]
    public void Execute_WhenPaginationPatternUsesCollectionPlaceholders_SlugifiesCollectionKey()
    {
        var posts = Enumerable.Range(1, 3)
            .Select(i => Item($"company-{i}", $"company-{i}", new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["collection"] = "Company Profiles"
            }, new DateTimeOffset(2026, 1, i, 0, 0, 0, TimeSpan.Zero)))
            .ToArray();
        var pipeline = new RoutePipeline();

        var result = pipeline.Execute(PaginatedConfig(
            collectionKey: "Company Profiles",
            listRoute: "/profiles/",
            urlPattern: "{collection}/{slug}/p/{page}",
            firstPageUsesListRoute: false), posts, TemplateResolver());

        var first = Assert.Single(result.ListRouteGraph.Routes, route => route.RouteId == "collection:Company Profiles:1");
        Assert.Equal("/profiles/company-profiles/company-profiles/p/1/", first.Url);
        Assert.Equal("/profiles/company-profiles/company-profiles/p/2/", first.NextUrl);

        var second = Assert.Single(result.ListRouteGraph.Routes, route => route.RouteId == "collection:Company Profiles:2");
        Assert.Equal("/profiles/company-profiles/company-profiles/p/2/", second.Url);
        Assert.Equal("/profiles/company-profiles/company-profiles/p/1/", second.PrevUrl);
    }

    [Fact]
    public void Execute_WhenMultipleCollectionsPaginated_GeneratesIndependentPageRoutes()
    {
        var posts = Enumerable.Range(1, 3)
            .Select(i => Item($"post-{i}", $"post-{i}", new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["collection"] = "post"
            }, new DateTimeOffset(2026, 1, i, 0, 0, 0, TimeSpan.Zero)));
        var companies = Enumerable.Range(1, 3)
            .Select(i => Item($"company-{i}", $"company-{i}", new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["collection"] = "company"
            }, new DateTimeOffset(2026, 2, i, 0, 0, 0, TimeSpan.Zero)));
        var pipeline = new RoutePipeline();

        var result = pipeline.Execute(MultiPaginatedConfig(), posts.Concat(companies).ToArray(), TemplateResolver());

        var postFirst = Assert.Single(result.ListRouteGraph.Routes, route => route.RouteId == "collection:post:1");
        Assert.Equal("/posts/", postFirst.Url);
        Assert.Equal("/posts/page/2/", postFirst.NextUrl);
        Assert.Equal(new[] { "post-3", "post-2" }, postFirst.Items.Select(item => item.Id));

        var postSecond = Assert.Single(result.ListRouteGraph.Routes, route => route.RouteId == "collection:post:2");
        Assert.Equal("/posts/page/2/", postSecond.Url);
        Assert.Equal("/posts/", postSecond.PrevUrl);
        Assert.Equal(new[] { "post-1" }, postSecond.Items.Select(item => item.Id));

        var companyFirst = Assert.Single(result.ListRouteGraph.Routes, route => route.RouteId == "collection:company:1");
        Assert.Equal("/companies/", companyFirst.Url);
        Assert.Equal("/companies/p/2/", companyFirst.NextUrl);
        Assert.Equal(new[] { "company-3", "company-2" }, companyFirst.Items.Select(item => item.Id));

        var companySecond = Assert.Single(result.ListRouteGraph.Routes, route => route.RouteId == "collection:company:2");
        Assert.Equal("/companies/p/2/", companySecond.Url);
        Assert.Equal("/companies/", companySecond.PrevUrl);
        Assert.Equal(new[] { "company-1" }, companySecond.Items.Select(item => item.Id));

        Assert.Contains(result.ListRoutes, route => route.Url == "/posts/page/2/");
        Assert.Contains(result.ListRoutes, route => route.Url == "/companies/p/2/");
    }

    [Fact]
    public void Execute_DistinctTypeAndCollectionGroupsListsPaginationAndFiltersByCollection()
    {
        var newsFeatured = Item("news-featured", "news-featured", new Dictionary<string, object>
        {
            ["type"] = "article", ["collection"] = "news", ["featured"] = "true"
        }, new DateTimeOffset(2026, 1, 2, 0, 0, 0, TimeSpan.Zero));
        var newsPlain = Item("news-plain", "news-plain", new Dictionary<string, object>
        {
            ["type"] = "article", ["collection"] = "news", ["featured"] = "false"
        }, new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var articleCollection = Item("article-collection", "article-collection", new Dictionary<string, object>
        {
            ["type"] = "page", ["collection"] = "article", ["featured"] = "true"
        });
        var config = new AppConfig
        {
            Site = new SiteConfig
            {
                Name = "test",
                Title = "Test",
                Collections = new Dictionary<string, CollectionConfig>(StringComparer.OrdinalIgnoreCase)
                {
                    ["news"] = new()
                    {
                        Permalink = "/news/{slug}/", Template = "pages/article.html",
                        ListRoute = "/news/", ListTemplate = "pages/article-list.html",
                        Pagination = new() { Enabled = true, PageSize = 1, UrlPattern = "page/{page}/" },
                        FilteredLists =
                        [
                            new FilteredListConfig
                            {
                                Field = "featured", Value = "true", ListRoute = "/news/featured/",
                                ListTemplate = "pages/featured.html"
                            }
                        ]
                    },
                    ["article"] = new()
                    {
                        Permalink = "/articles/{slug}/", Template = "pages/article.html",
                        ListRoute = "/articles/", ListTemplate = "pages/article-list.html"
                    }
                }
            },
            Content = TestContent.Markdown()
        };

        var result = new RoutePipeline().Execute(config, [newsFeatured, newsPlain, articleCollection], TemplateResolver());

        var newsPage1 = Assert.Single(result.ListRouteGraph.Routes, route => route.RouteId == "collection:news:1");
        var newsPage2 = Assert.Single(result.ListRouteGraph.Routes, route => route.RouteId == "collection:news:2");
        var filtered = Assert.Single(result.ListRouteGraph.Routes, route => route.RouteId == "filter:news:featured:true:1");
        Assert.Equal(["news-featured"], newsPage1.Items.Select(item => item.Id));
        Assert.Equal(["news-plain"], newsPage2.Items.Select(item => item.Id));
        Assert.Equal(["news-featured"], filtered.Items.Select(item => item.Id));
        Assert.DoesNotContain(articleCollection.Id, newsPage1.Items.Concat(newsPage2.Items).Select(item => item.Id));
    }

    private static ThemeTemplateResolver TemplateResolver()
    {
        var manifest = new ThemeManifestV2
        {
            Templates = new Dictionary<string, ThemeTemplateDefinition>(StringComparer.OrdinalIgnoreCase)
            {
                ["page"] = new()
                {
                    Template = "content/page.html",
                    Accepts = new ThemeTemplateAccept { Type = "page" }
                },
                ["list"] = new()
                {
                    Template = "indexes/list.html",
                    Accepts = new ThemeTemplateAccept { Kind = "list" }
                }
            }
        };

        return new ThemeTemplateResolver(manifest);
    }

    private static AppConfig Config()
    {
        return new AppConfig
        {
            Site = new SiteConfig
            {
                Name = "test",
                Title = "Test",
                Collections = new Dictionary<string, CollectionConfig>(StringComparer.OrdinalIgnoreCase)
                {
                    ["article"] = new CollectionConfig
                    {
                        Permalink = "/articles/{slug}/",
                        Template = "pages/article.html",
                        ListRoute = "/articles/",
                        ListTemplate = "pages/article-list.html",
                        FilteredLists = new[]
                        {
                            new FilteredListConfig
                            {
                                Field = "featured",
                                Value = "true",
                                ListRoute = "/articles/featured/",
                                ListTemplate = "pages/featured.html"
                            }
                        }
                    },
                    ["page"] = new CollectionConfig
                    {
                        Permalink = "/pages/{slug}/"
                    }
                }
            },
            Content = TestContent.Markdown()
        };
    }

    private static AppConfig MultiPaginatedConfig()
    {
        return new AppConfig
        {
            Site = new SiteConfig
            {
                Name = "test",
                Title = "Test",
                Collections = new Dictionary<string, CollectionConfig>(StringComparer.OrdinalIgnoreCase)
                {
                    ["post"] = new CollectionConfig
                    {
                        Permalink = "/posts/{slug}/",
                        Template = "pages/article.html",
                        ListRoute = "/posts/",
                        ListTemplate = "pages/article-list.html",
                        Pagination = new CollectionPaginationConfig
                        {
                            Enabled = true,
                            PageSize = 2,
                            UrlPattern = "page/{page}/"
                        }
                    },
                    ["company"] = new CollectionConfig
                    {
                        Permalink = "/companies/{slug}/",
                        Template = "pages/article.html",
                        ListRoute = "/companies/",
                        ListTemplate = "pages/article-list.html",
                        Pagination = new CollectionPaginationConfig
                        {
                            Enabled = true,
                            PageSize = 2,
                            UrlPattern = "p/{page}/"
                        }
                    }
                }
            },
            Content = TestContent.Markdown()
        };
    }

    private static AppConfig FilteredPaginatedConfig(
        int pageSize = 2,
        string urlPattern = "page/{page}/",
        string emptyBehavior = "render")
    {
        return new AppConfig
        {
            Site = new SiteConfig
            {
                Name = "test",
                Title = "Test",
                Collections = new Dictionary<string, CollectionConfig>(StringComparer.OrdinalIgnoreCase)
                {
                    ["company"] = new CollectionConfig
                    {
                        Permalink = "/companies/{slug}/",
                        Template = "pages/company.html",
                        ListRoute = "/companies/",
                        ListTemplate = "pages/company-list.html",
                        FilteredLists = new[]
                        {
                            new FilteredListConfig
                            {
                                Field = "country",
                                Value = "Malaysia",
                                ListRoute = "/companies/malaysia/",
                                ListTemplate = "pages/company-filter.html",
                                PageSize = pageSize,
                                UrlPattern = urlPattern,
                                EmptyBehavior = emptyBehavior
                            }
                        }
                    }
                }
            },
            Content = TestContent.Markdown()
        };
    }

    private static AppConfig FilteredOperatorConfig()
    {
        return new AppConfig
        {
            Site = new SiteConfig
            {
                Name = "test",
                Title = "Test",
                Collections = new Dictionary<string, CollectionConfig>(StringComparer.OrdinalIgnoreCase)
                {
                    ["company"] = new CollectionConfig
                    {
                        Permalink = "/companies/{slug}/",
                        Template = "pages/company.html",
                        ListRoute = "/companies/",
                        ListTemplate = "pages/company-list.html",
                        FilteredLists = new[]
                        {
                            new FilteredListConfig
                            {
                                Field = "category",
                                Operator = "in",
                                Values = new[] { "市场观察", "政策动态" },
                                ListRoute = "/companies/market/",
                                ListTemplate = "pages/company-filter.html"
                            }
                        }
                    }
                }
            },
            Content = TestContent.Markdown()
        };
    }

    private static AppConfig FilteredContainsConfig()
    {
        return new AppConfig
        {
            Site = new SiteConfig
            {
                Name = "test",
                Title = "Test",
                Collections = new Dictionary<string, CollectionConfig>(StringComparer.OrdinalIgnoreCase)
                {
                    ["company"] = new CollectionConfig
                    {
                        Permalink = "/companies/{slug}/",
                        Template = "pages/company.html",
                        ListRoute = "/companies/",
                        ListTemplate = "pages/company-list.html",
                        FilteredLists = new[]
                        {
                            new FilteredListConfig
                            {
                                Field = "industry",
                                Operator = "contains",
                                Value = "logistics",
                                ListRoute = "/companies/logistics/",
                                ListTemplate = "pages/company-filter.html"
                            }
                        }
                    }
                }
            },
            Content = TestContent.Markdown()
        };
    }

    private static AppConfig FilteredUsesCollectionPaginationConfig()
    {
        return new AppConfig
        {
            Site = new SiteConfig
            {
                Name = "test",
                Title = "Test",
                Collections = new Dictionary<string, CollectionConfig>(StringComparer.OrdinalIgnoreCase)
                {
                    ["company"] = new CollectionConfig
                    {
                        Permalink = "/companies/{slug}/",
                        Template = "pages/company.html",
                        ListRoute = "/companies/",
                        ListTemplate = "pages/company-list.html",
                        Pagination = new CollectionPaginationConfig
                        {
                            Enabled = true,
                            PageSize = 2,
                            UrlPattern = "p/{page}/"
                        },
                        FilteredLists = new[]
                        {
                            new FilteredListConfig
                            {
                                Field = "country",
                                Value = "Malaysia",
                                ListRoute = "/companies/malaysia/",
                                ListTemplate = "pages/company-filter.html"
                            }
                        }
                    }
                }
            },
            Content = TestContent.Markdown()
        };
    }

    private static AppConfig PaginatedConfig(
        string collectionKey = "article",
        string listRoute = "/articles/",
        string urlPattern = "page/:num/",
        bool firstPageUsesListRoute = true)
    {
        return new AppConfig
        {
            Site = new SiteConfig
            {
                Name = "test",
                Title = "Test",
                Collections = new Dictionary<string, CollectionConfig>(StringComparer.OrdinalIgnoreCase)
                {
                    [collectionKey] = new CollectionConfig
                    {
                        Permalink = $"{RoutePathBuilder.NormalizeListRoute(listRoute)}{{slug}}/",
                        Template = "pages/article.html",
                        ListRoute = listRoute,
                        ListTemplate = "pages/article-list.html",
                        Pagination = new CollectionPaginationConfig
                        {
                            Enabled = true,
                            PageSize = 2,
                            UrlPattern = urlPattern,
                            FirstPageUsesListRoute = firstPageUsesListRoute
                        }
                    }
                }
            },
            Content = TestContent.Markdown()
        };
    }

    private static ContentDocument Item(
        string id,
        string slug,
        IReadOnlyDictionary<string, object> fieldValues,
        DateTimeOffset? publishAt = null)
    {
        return ContentDocument.Create(id, id, slug, publishAt ?? DateTimeOffset.UnixEpoch, $"<p>{id}</p>", ContentFieldReader.ToFieldMap(fieldValues));
    }

    private static string Normalize(string path)
    {
        return path.Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');
    }
}
