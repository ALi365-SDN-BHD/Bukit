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

        Assert.Equal(new[] { post.Id, page.Id }, result.ContentDocuments.Select(x => x.Id));
        Assert.Equal(2, result.RoutedDocuments.Count);
        Assert.Equal(post.Id, result.RoutedDocuments[0].Document.Id);
        Assert.Equal("/articles/hello/", result.RoutedDocuments[0].Route.Url);
        Assert.Equal("articles/hello/index.html", Normalize(result.RoutedDocuments[0].Route.OutputPath));
        Assert.Equal("pages/article.html", result.RoutedDocuments[0].Route.Template);
        Assert.Equal(page.Id, result.RoutedDocuments[1].Document.Id);
        Assert.Equal("/pages/about/", result.RoutedDocuments[1].Route.Url);
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

    private static ContentDocument Item(string id, string slug, IReadOnlyDictionary<string, object> meta)
    {
        return ContentDocument.Create(id, id, slug, DateTimeOffset.UnixEpoch, $"<p>{id}</p>", ContentFieldReader.ToFieldMap(meta));
    }

    private static string Normalize(string path)
    {
        return path.Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');
    }
}
