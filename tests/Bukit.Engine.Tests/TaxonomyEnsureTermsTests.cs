using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Engine.Plugins;
using Bukit.Engine.Plugins.BuiltIn;
using Bukit.Routing;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Shared;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class TaxonomyEnsureTermsTests
{
    [Fact]
    public void DerivePages_WithEnsureTerms_GeneratesEmptyCategoryTermPage()
    {
        var layoutsDir = CreateTaxonomyLayoutsDir();
        var ctx = new BuildContext
        {
            Config = new AppConfig
            {
                Site = new SiteConfig { Name = "t", Title = "t" },
                Content = new ContentConfig { Provider = "markdown" }
            },
            RootDir = "C:\\",
            OutputDir = "C:\\out",
            BaseUrl = "/",
            LayoutsDir = layoutsDir,
            RoutedDocuments = Array.Empty<RoutedContentDocument>(),
            TemplateResolver = ResolveTemplateKind,
            Logger = new ConsoleLogger(LogLevel.Error)
        };

        ctx.Data["taxonomy_ensure_terms"] = new Dictionary<string, List<Dictionary<string, object>>>(StringComparer.OrdinalIgnoreCase)
        {
            ["categories"] = new List<Dictionary<string, object>>
            {
                new(StringComparer.OrdinalIgnoreCase)
                {
                    ["title"] = "Cat One",
                    ["slug"] = "cat-one"
                }
            }
        };

        var plugin = new TaxonomyPlugin();
        var derived = plugin.DerivePages(ctx);

        Assert.Contains(derived, x => x.Route.Url == "/categories/");
        var term = Assert.Single(derived, x => x.Route.Url == "/categories/cat-one/");

        var fields = term.Document.CustomFields;
        Assert.NotNull(fields);
        Assert.True(fields!.ContainsKey("items"));
        var itemsField = fields["items"];
        Assert.Equal("list", itemsField.Type);
        var list = Assert.IsType<List<object>>(itemsField.Value);
        Assert.Empty(list);
    }

    [Fact]
    public void SiteEngine_ExtractsEnsureTermsFromDataItems()
    {
        var ctx = new BuildContext
        {
            Config = new AppConfig
            {
                Site = new SiteConfig { Name = "t", Title = "t" },
                Content = new ContentConfig { Provider = "markdown" }
            },
            RootDir = "C:\\",
            OutputDir = "C:\\out",
            BaseUrl = "/",
            LayoutsDir = "C:\\layouts",
            RoutedDocuments = Array.Empty<RoutedContentDocument>(),
            Logger = new ConsoleLogger(LogLevel.Error)
        };

        var dataDocument = ContentDocument.Create(
            "c1",
            "Cat One",
            "cat-one",
            DateTimeOffset.UtcNow,
            string.Empty,
            ContentFieldReader.ToFieldMap(new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["sourceKey"] = "categories",
                ["sourceMode"] = "data"
            }));

        TaxonomyTermsInjector.InjectFromDataDocuments(ctx, new List<ContentDocument> { dataDocument });

        Assert.True(ctx.Data.TryGetValue("taxonomy_ensure_terms", out var obj));
        var map = Assert.IsType<Dictionary<string, List<Dictionary<string, object>>>>(obj);
        Assert.True(map.TryGetValue("categories", out var list));
        Assert.Single(list);
        Assert.Equal("cat-one", list[0]["slug"]);
        Assert.Equal("Cat One", list[0]["title"]);
    }

    [Fact]
    public void TaxonomyPlugin_ReusesIndexCache_AcrossDeriveAndAfterBuild()
    {
        TaxonomyPlugin.ResetBuildIndexCountForTests();
        var layoutsDir = CreateTaxonomyLayoutsDir();
        var ctx = new BuildContext
        {
            Config = new AppConfig
            {
                Site = new SiteConfig { Name = "t", Title = "t" },
                Content = new ContentConfig { Provider = "markdown" }
            },
            RootDir = "C:\\",
            OutputDir = Path.Combine(Path.GetTempPath(), "bukit-taxonomy-tests", Guid.NewGuid().ToString("N")),
            BaseUrl = "/",
            LayoutsDir = layoutsDir,
            RoutedDocuments = new List<(ContentDocument Item, RouteInfo Route)>
            {
                (
                    ContentDocument.Create(
                        id: "p1",
                        title: "Post 1",
                        slug: "post-1",
                        publishAt: DateTimeOffset.UtcNow,
                        contentHtml: "<p>hello</p>",
                        fields: ContentFieldReader.ToFieldMap(new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["tags"] = new[] { "alpha" },
                            ["categories"] = new[] { "news" }
                        })),
                    new RouteInfo("/blog/post-1/", "blog/post-1/index.html", "pages/post.html")
                )
            }.ToRoutedDocuments(),
            TemplateResolver = ResolveTemplateKind,
            Logger = new ConsoleLogger(LogLevel.Error)
        };

        Directory.CreateDirectory(ctx.OutputDir);
        try
        {
            var plugin = new TaxonomyPlugin();
            _ = plugin.DerivePages(ctx);
            plugin.AfterBuild(ctx);
        }
        finally
        {
            if (Directory.Exists(ctx.OutputDir))
            {
                Directory.Delete(ctx.OutputDir, recursive: true);
            }
        }

        Assert.Equal(2, TaxonomyPlugin.BuildIndexCountForTests);
    }

    private static string ResolveTemplateKind(string kind)
        => kind.Trim().ToLowerInvariant() switch
        {
            "taxonomy_index" => "pages/taxonomy-index.html",
            "taxonomy_term" => "pages/taxonomy-term.html",
            _ => throw new ConfigException($"Unexpected template kind: {kind}")
        };

    private static string CreateTaxonomyLayoutsDir()
    {
        var layoutsDir = Path.Combine(Path.GetTempPath(), "bukit-taxonomy-tests-" + Guid.NewGuid().ToString("N"), "layouts");
        Directory.CreateDirectory(Path.Combine(layoutsDir, "pages"));
        File.WriteAllText(Path.Combine(layoutsDir, "pages", "taxonomy-index.html"), "{{ page.content }}");
        File.WriteAllText(Path.Combine(layoutsDir, "pages", "taxonomy-term.html"), "{{ page.content }}");
        return layoutsDir;
    }
}
