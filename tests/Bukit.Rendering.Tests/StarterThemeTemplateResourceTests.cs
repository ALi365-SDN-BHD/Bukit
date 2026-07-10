using Bukit.Engine.Abstractions.Content;
using Bukit.Rendering.Scriban;
using Bukit.Theme;
using Xunit;

namespace Bukit.Rendering.Tests;

public sealed class StarterThemeTemplateResourceTests : IDisposable
{
    private readonly string _layoutsDir;

    public StarterThemeTemplateResourceTests()
    {
        _layoutsDir = Path.Combine(Path.GetTempPath(), "bukit-starter-template-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(_layoutsDir, "layouts"));
        Directory.CreateDirectory(Path.Combine(_layoutsDir, "partials"));
        Directory.CreateDirectory(Path.Combine(_layoutsDir, "pages"));
        CopyResource("BaseLayout.html", "layouts/base.html");
        CopyResource("ListCardPartial.html", "partials/list-card.html");
        CopyResource("PaginationNavPartial.html", "partials/pagination-nav.html");
        CopyResource("ListTemplate.html", "pages/list.html");
        CopyResource("PaginationTemplate.html", "pages/pagination.html");
        CopyResource("TaxonomyTermTemplate.html", "pages/taxonomy-term.html");
    }

    public void Dispose()
    {
        if (Directory.Exists(_layoutsDir))
        {
            Directory.Delete(_layoutsDir, recursive: true);
        }
    }

    [Theory]
    [InlineData("pages/list.html")]
    [InlineData("pages/pagination.html")]
    [InlineData("pages/taxonomy-term.html")]
    public void StarterListTemplates_RenderFromStableListModel(string template)
    {
        var renderer = new ScribanTemplateRenderer(_layoutsDir);

        var html = renderer.RenderList(template, CreateListModel());

        Assert.Contains("Market Dispatch", html, StringComparison.Ordinal);
        Assert.Contains("Market Watch", html, StringComparison.Ordinal);
        Assert.Contains("Malaysia", html, StringComparison.Ordinal);
        Assert.Contains("Logistics", html, StringComparison.Ordinal);
        Assert.Contains("href=\"/archive/page/2/\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("page.fields.items.value", html, StringComparison.Ordinal);
        Assert.DoesNotContain("page.fields.pagination.value", html, StringComparison.Ordinal);
    }

    [Fact]
    public void StarterThemeManifest_LoadsWithStrictCurrentContract()
    {
        var themeRoot = Path.Combine(Path.GetTempPath(), "bukit-starter-manifest-tests-" + Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(themeRoot);
            var source = Path.Combine(FindRepositoryRoot(), "src", "Bukit-Core", "Bukit.Cli", "Resources", "StarterTheme", "ThemeYaml.yaml");
            File.Copy(source, Path.Combine(themeRoot, "theme.yaml"));

            var manifest = ThemeManifestLoader.Load(themeRoot, required: true);

            Assert.NotNull(manifest);
            Assert.Equal("starter", manifest.Name);
            Assert.True(manifest.Templates?.ContainsKey("list"));
            Assert.Contains("assets/style.css", manifest.Assets?.Css ?? []);
        }
        finally
        {
            if (Directory.Exists(themeRoot))
            {
                Directory.Delete(themeRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void StarterTaxonomyTermTemplate_UsesRoutePrefixForAncestorBreadcrumb()
    {
        var renderer = new ScribanTemplateRenderer(_layoutsDir);

        var html = renderer.RenderPage("pages/taxonomy-term.html", CreateTaxonomyPageModel());

        Assert.Contains("<title>Market Watch | Starter Test</title>", html, StringComparison.Ordinal);
        Assert.Contains("href=\"/insights/category/market/\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("href=\"/category/market/\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public void StarterBaseLayout_FallsBackForLegacySeoModelsAndEscapesEveryTitleBranch()
    {
        var renderer = new ScribanTemplateRenderer(_layoutsDir);
        var site = new SiteModel { Name = "starter-test", Title = "Starter Test", BaseUrl = "", Language = "en" };

        var documentTitleHtml = renderer.RenderPage("layouts/base.html", new PageModel
        {
            Site = site,
            Page = new PageInfo
            {
                Title = "Page",
                Url = "/page/",
                Content = "",
                Seo = new SeoModel
                {
                    Title = "Semantic",
                    DocumentTitle = "Document & </title>",
                    Canonical = "https://example.com/page/"
                }
            }
        });
        var legacySeoHtml = renderer.RenderPage("layouts/base.html", new PageModel
        {
            Site = site,
            Page = new PageInfo
            {
                Title = "Page",
                Url = "/legacy/",
                Content = "",
                Seo = new SeoModel
                {
                    Title = "Legacy & </title>",
                    Canonical = "https://example.com/legacy/"
                }
            }
        });
        var pageTitleHtml = renderer.RenderPage("layouts/base.html", new PageModel
        {
            Site = site,
            Page = new PageInfo
            {
                Title = "Page & </title>",
                Url = "/plain/",
                Content = ""
            }
        });

        Assert.Contains("<title>Document &amp; &lt;/title&gt;</title>", documentTitleHtml, StringComparison.Ordinal);
        Assert.Contains("<title>Legacy &amp; &lt;/title&gt;</title>", legacySeoHtml, StringComparison.Ordinal);
        Assert.Contains("<title>Page &amp; &lt;/title&gt;</title>", pageTitleHtml, StringComparison.Ordinal);
    }

    private static ListPageModel CreateListModel()
    {
        var item = new PageInfo
        {
            Title = "Market Dispatch",
            Url = "/insights/market-dispatch/",
            Summary = "Build-time business insight.",
            Content = "",
            PublishDate = DateTimeOffset.Parse("2026-05-03T00:00:00Z"),
            Fields = new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
            {
                ["categories"] = new("list", new[] { "Market Watch" }),
                ["tags"] = new("list", new[] { "logistics" }),
                ["country"] = new("text", "Malaysia"),
                ["industry"] = new("text", "Logistics")
            }
        };

        return new ListPageModel
        {
            Site = new SiteModel
            {
                Name = "starter-test",
                Title = "Starter Test",
                BaseUrl = "",
                Language = "en"
            },
            Page = new PageInfo
            {
                Title = "Archive",
                Url = "/archive/",
                Content = ""
            },
            Pages = new[] { item },
            Items = new[] { item },
            Pagination = new ListPaginationModel
            {
                Page = 1,
                PageSize = 1,
                TotalPages = 2,
                TotalItems = 2,
                HasNext = true,
                NextUrl = "/archive/page/2/"
            },
            Collection = new ListCollectionModel { Key = "insight" },
            Taxonomy = new ListTaxonomyModel
            {
                Kind = "category",
                Term = "Market Watch",
                Slug = "market-watch"
            }
        };
    }

    private static PageModel CreateTaxonomyPageModel()
    {
        return new PageModel
        {
            Site = new SiteModel
            {
                Name = "starter-test",
                Title = "Starter Test",
                BaseUrl = "",
                Language = "en"
            },
            Page = new PageInfo
            {
                Title = "Market Watch",
                Url = "/insights/category/market-watch/",
                Content = "",
                Seo = new SeoModel
                {
                    Title = "Market Watch",
                    DocumentTitle = "Market Watch | Starter Test",
                    Canonical = "https://example.com/insights/category/market-watch/"
                },
                Fields = new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
                {
                    ["type"] = new("text", "derived"),
                    ["items"] = new("list", new[]
                    {
                        new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["title"] = "Market Dispatch",
                            ["url"] = "/insights/market-dispatch/"
                        }
                    }),
                    ["taxonomy"] = new("object", new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["kind"] = "category",
                        ["term"] = "Market Watch",
                        ["slug"] = "market-watch",
                        ["route_prefix"] = "/insights/category",
                        ["ancestors"] = new[] { "market" }
                    })
                }
            }
        };
    }

    private void CopyResource(string resourceName, string relativeTarget)
    {
        var source = Path.Combine(FindRepositoryRoot(), "src", "Bukit-Core", "Bukit.Cli", "Resources", "StarterTheme", resourceName);
        var target = Path.Combine(_layoutsDir, relativeTarget);
        File.WriteAllText(target, File.ReadAllText(source));
    }

    private static string FindRepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "src")) &&
                Directory.Exists(Path.Combine(dir.FullName, "tests")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Unable to locate repository root from test runtime path.");
    }
}
