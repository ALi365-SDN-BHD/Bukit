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
            ["collection"] = "article"
        });
        var page = Item("about", "about", new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["type"] = "page",
            ["collection"] = "page"
        });
        var data = Item("settings", "settings", new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["sourceMode"] = "data"
        });
        var pipeline = new RoutePipeline();

        var result = pipeline.Execute(Config(), new[] { post, page, data }, TemplateResolver());

        Assert.Equal(new[] { post, page }, result.ContentItems);
        Assert.Equal(2, result.Routed.Count);
        Assert.Same(post, result.Routed[0].Item);
        Assert.Equal("/articles/hello/", result.Routed[0].Route.Url);
        Assert.Equal("articles/hello/index.html", Normalize(result.Routed[0].Route.OutputPath));
        Assert.Equal("pages/article.html", result.Routed[0].Route.Template);
        Assert.Same(page, result.Routed[1].Item);
        Assert.Equal("/pages/about/", result.Routed[1].Route.Url);
        Assert.Contains(result.ListRoutes, route => route.Url == "/" && route.OutputPath == "index.html");
        Assert.Contains(result.ListRoutes, route => route.Url == "/articles/" && Normalize(route.OutputPath) == "articles/index.html" && route.Template == "pages/article-list.html");
        Assert.Contains(result.ListRoutes, route => route.Url == "/articles/featured/" && Normalize(route.OutputPath) == "articles/featured/index.html" && route.Template == "pages/featured.html");
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
            Content = new ContentConfig { Provider = "markdown" }
        }, new[] { first, second }));

        Assert.Contains("Route conflict", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ExecuteDocuments_GeneratesRoutesFromTypedPoliciesAndFiltersDataDocuments()
    {
        var post = Document("hello", "hello", "post", "article", isDataModule: false);
        var page = Document("about", "about", "page", "page", isDataModule: false);
        var data = Document("settings", "settings", "data", "settings", isDataModule: true);
        var pipeline = new RoutePipeline();

        var result = pipeline.ExecuteDocuments(Config(), new[] { post, page, data }, TemplateResolver());

        Assert.Equal(new[] { post, page }, result.ContentDocuments);
        Assert.Equal(2, result.RoutedDocuments.Count);
        Assert.Same(post, result.RoutedDocuments[0].Document);
        Assert.Equal("/articles/hello/", result.RoutedDocuments[0].Route.Url);
        Assert.Equal("articles/hello/index.html", Normalize(result.RoutedDocuments[0].Route.OutputPath));
        Assert.Equal("pages/article.html", result.RoutedDocuments[0].Route.Template);
        Assert.Same(page, result.RoutedDocuments[1].Document);
        Assert.Equal("/pages/about/", result.RoutedDocuments[1].Route.Url);
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
            Content = new ContentConfig
            {
                Provider = "markdown"
            }
        };
    }

    private static ContentItem Item(string id, string slug, IReadOnlyDictionary<string, object> meta)
    {
        return new ContentItem(id, id, slug, DateTimeOffset.UnixEpoch, $"<p>{id}</p>", meta);
    }

    private static ContentDocument Document(
        string id,
        string slug,
        string type,
        string collection,
        bool isDataModule)
    {
        var record = new ContentRecord(
            Identity: new ContentIdentity(id, slug, id, type, "published"),
            Presentation: new ContentPresentation(id, null, $"<p>{id}</p>", "en", Array.Empty<string>()),
            Classification: new ContentClassification(type, collection, Array.Empty<string>(), Array.Empty<string>()),
            Ownership: new ContentOwnership(null, null, null, null),
            Lifecycle: new ContentLifecycle(DateTimeOffset.UnixEpoch, null, null, null),
            Provenance: new ProvenanceRecord(null, null, Array.Empty<string>(), Array.Empty<string>(), null),
            Trust: new TrustMetadata(null, "unreviewed", Array.Empty<string>()),
            Entities: Array.Empty<EntityRecord>(),
            Relations: Array.Empty<ContentRelation>(),
            Media: Array.Empty<MediaAsset>());

        return new ContentDocument(
            record,
            new ContentBodyRef($"<p>{id}</p>", null, null, null),
            new ContentRoutePolicy(null, null, null, null, collection),
            new ContentPublishPolicy(false, false, false, false, false, false, isDataModule),
            new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase),
            Array.Empty<ContentDiagnostic>());
    }

    private static string Normalize(string path)
    {
        return path.Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');
    }
}
