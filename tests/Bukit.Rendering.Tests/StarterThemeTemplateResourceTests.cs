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
        CopyResource("SearchTemplate.html", "pages/search.html");
        CopyResource("HeaderPartial.html", "partials/header.html");
        CopyResource("TaxonomyIndexTemplate.html", "pages/taxonomy-index.html");
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
        Assert.Contains("logistics", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Malaysia", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Logistics", html, StringComparison.Ordinal);
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
    public void StarterTheme_DoesNotContainAnalyticsPartial()
    {
        var path = Path.Combine(
            FindRepositoryRoot(),
            "src", "Bukit-Core", "Bukit.Cli", "Resources", "StarterTheme", "AnalyticsPartial.html");

        Assert.False(File.Exists(path));
    }

    [Fact]
    public void StarterTheme_DoesNotContainLegacyBrandPlaceholder()
    {
        var source = Path.Combine(
            FindRepositoryRoot(),
            "src", "Bukit-Core", "Bukit.Cli", "Resources", "StarterTheme",
            "HeaderPartial.html");
        var header = File.ReadAllText(source);

        Assert.DoesNotContain("{{--", header, StringComparison.Ordinal);
    }

    [Fact]
    public void StarterHeaderFooterAndIndex_EscapeSiteSettings()
    {
        CopyResource("HeaderPartial.html", "partials/header.html");
        CopyResource("FooterPartial.html", "partials/footer.html");
        CopyResource("IndexTemplate.html", "pages/index.html");
        var renderer = new ScribanTemplateRenderer(_layoutsDir);
        var site = new SiteModel
        {
            Name = "starter-test",
            Title = "A & B",
            Description = "About & more",
            BaseUrl = "",
            Language = "en",
            Params = new Dictionary<string, object>
            {
                ["brand"] = "A & B",
                ["footer_text"] = "A & B",
                ["latest_heading"] = "Latest & best"
            }
        };
        var page = new PageModel
        {
            Site = site,
            Page = new PageInfo { Title = "Home", Url = "/", Content = "" }
        };

        var header = renderer.RenderPage("partials/header.html", page);
        var footer = renderer.RenderPage("partials/footer.html", page);
        var index = renderer.RenderPage("pages/index.html", page);

        Assert.Contains("A &amp; B", header, StringComparison.Ordinal);
        Assert.Contains("A &amp; B", footer, StringComparison.Ordinal);
        Assert.Contains("A &amp; B", index, StringComparison.Ordinal);
        Assert.Contains("About &amp; more", index, StringComparison.Ordinal);
        Assert.Contains("Latest &amp; best", index, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("/docs", "/about/", "href=\"/docs/about/\"")]
    [InlineData("/", "/about/", "href=\"/about/\"")]
    [InlineData("", "/about/", "href=\"/about/\"")]
    [InlineData("/docs", "https://example.com/", "href=\"https://example.com/\"")]
    [InlineData("/docs", "//cdn.example.com/x", "href=\"//cdn.example.com/x\"")]
    public void StarterHeader_PrefixesRootRelativeNavigationLinks(string baseUrl, string link, string expected)
    {
        var renderer = new ScribanTemplateRenderer(_layoutsDir);
        var site = new SiteModel
        {
            Name = "starter-test",
            Title = "Starter Test",
            BaseUrl = baseUrl,
            Language = "en",
            Modules = new Dictionary<string, IReadOnlyList<ModuleInfo>>
            {
                ["navigation"] =
                [
                    new ModuleInfo
                    {
                        Id = "nav-1",
                        Title = "About & <us>",
                        Slug = "about",
                        Content = "",
                        Fields = new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["link"] = new("text", link)
                        }
                    }
                ]
            }
        };

        var html = renderer.RenderPage("partials/header.html", new PageModel
        {
            Site = site,
            Page = new PageInfo { Title = "Home", Url = "/", Content = "" }
        });

        Assert.Contains(expected, html, StringComparison.Ordinal);
        Assert.DoesNotContain("href=\"//about/\"", html, StringComparison.Ordinal);
        Assert.Contains("About &amp; &lt;us&gt;", html, StringComparison.Ordinal);
    }

    [Fact]
    public void StarterTaxonomyIndex_PrefixesTermLinksAndEscapesText()
    {
        var renderer = new ScribanTemplateRenderer(_layoutsDir);
        var page = new PageModel
        {
            Site = new SiteModel { Name = "starter-test", Title = "Starter Test", BaseUrl = "/docs", Language = "en" },
            Page = new PageInfo
            {
                Title = "Tags <all>",
                Url = "/tags/",
                Content = "",
                Fields = new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
                {
                    ["terms"] = new("list", new[]
                    {
                        new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["title"] = "News & <b>",
                            ["url"] = "/tags/news/",
                            ["count"] = 2,
                            ["description"] = "Desc <i>"
                        }
                    })
                }
            }
        };

        var html = renderer.RenderPage("pages/taxonomy-index.html", page);

        Assert.Contains("href=\"/docs/tags/news/\"", html, StringComparison.Ordinal);
        Assert.Contains("News &amp; &lt;b&gt;", html, StringComparison.Ordinal);
        Assert.Contains("Desc &lt;i&gt;", html, StringComparison.Ordinal);
        Assert.Contains("Tags &lt;all&gt;", html, StringComparison.Ordinal);
        Assert.Contains("2 items", html, StringComparison.Ordinal);
    }

    [Fact]
    public void StarterListCard_EscapesContentText()
    {
        var renderer = new ScribanTemplateRenderer(_layoutsDir);
        var model = CreateListModel();
        var item = model.Pages[0] with { Title = "A <b> & C", Summary = "S <i>" };

        var html = renderer.RenderList("pages/list.html", model with { Pages = [item], Items = [item] });

        Assert.Contains("A &lt;b&gt; &amp; C", html, StringComparison.Ordinal);
        Assert.Contains("S &lt;i&gt;", html, StringComparison.Ordinal);
        Assert.DoesNotContain("A <b> & C", html, StringComparison.Ordinal);
    }

    [Fact]
    public void StarterHeader_FallsBackToEnglishLabelsAndHidesMenuToggleWithoutScript()
    {
        var renderer = new ScribanTemplateRenderer(_layoutsDir);

        var html = renderer.RenderPage("partials/header.html", new PageModel
        {
            Site = new SiteModel { Name = "starter-test", Title = "Starter Test", BaseUrl = "", Language = "en" },
            Page = new PageInfo { Title = "Home", Url = "/", Content = "" }
        });

        Assert.Contains(">Home</a>", html, StringComparison.Ordinal);
        Assert.Contains(">Blog</a>", html, StringComparison.Ordinal);
        Assert.Contains("aria-controls=\"nav-links\" hidden>Menu</button>", html, StringComparison.Ordinal);
        Assert.Contains("id=\"nav-links\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public void StarterHeader_UsesAndEscapesConfiguredLabels()
    {
        var renderer = new ScribanTemplateRenderer(_layoutsDir);
        var site = new SiteModel
        {
            Name = "starter-test",
            Title = "Starter Test",
            BaseUrl = "",
            Language = "zh-CN",
            Params = new Dictionary<string, object>
            {
                ["labels"] = new Dictionary<string, object> { ["home"] = "首页 & <x>" }
            }
        };

        var html = renderer.RenderPage("partials/header.html", new PageModel
        {
            Site = site,
            Page = new PageInfo { Title = "Home", Url = "/", Content = "" }
        });

        Assert.Contains(">首页 &amp; &lt;x&gt;</a>", html, StringComparison.Ordinal);
        Assert.Contains(">Blog</a>", html, StringComparison.Ordinal);
    }

    [Fact]
    public void StarterSearchTemplate_EscapesLabelsInScriptStrings()
    {
        var renderer = new ScribanTemplateRenderer(_layoutsDir);
        var site = new SiteModel
        {
            Name = "starter-test",
            Title = "Starter Test",
            BaseUrl = "",
            Language = "en",
            Params = new Dictionary<string, object>
            {
                ["labels"] = new Dictionary<string, object>
                {
                    ["untitled"] = "\"</script><script>alert(1)//",
                    ["search"] = "Find \"<x>"
                }
            }
        };

        var html = renderer.RenderPage("pages/search.html", new PageModel
        {
            Site = site,
            Page = new PageInfo { Title = "Search", Url = "/search/", Content = "" }
        });

        Assert.Contains("const untitledLabel = \"\\\"\\u003C/script>\\u003Cscript>alert(1)//\";", html, StringComparison.Ordinal);
        Assert.Contains(">Find &quot;&lt;x&gt;</button>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("</script><script>alert", html, StringComparison.Ordinal);
    }

    [Fact]
    public void StarterStylesheet_KeepsColorOverrideContractAndDarkMode()
    {
        var css = File.ReadAllText(StarterThemePath("StyleCss.css"));

        Assert.Contains("--primary: #0b5fff;", css, StringComparison.Ordinal);
        Assert.Contains("--accent: #0f7b6c;", css, StringComparison.Ordinal);
        Assert.Contains("prefers-color-scheme: dark", css, StringComparison.Ordinal);
        Assert.Contains("@supports (color: color-mix(", css, StringComparison.Ordinal);
        Assert.Contains(".nav-toggle:hover", css, StringComparison.Ordinal);
        Assert.DoesNotContain("min-width: 681px", css, StringComparison.Ordinal);
    }

    [Fact]
    public void StarterTheme_LoadsNoExternalStylesheetsFontsOrScripts()
    {
        foreach (var file in Directory.EnumerateFiles(StarterThemePath(""), "*.*")
            .Where(path => path.EndsWith(".html", StringComparison.Ordinal) || path.EndsWith(".css", StringComparison.Ordinal)))
        {
            var text = File.ReadAllText(file);
            Assert.DoesNotMatch(@"<(script|link)[^>]+(src|href)=""(https?:)?//", text);
            Assert.DoesNotMatch(@"@import|url\((""|')?(https?:)?//", text);
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

    [Fact]
    public void StarterBaseLayout_EscapesSiteLanguageAttribute()
    {
        var renderer = new ScribanTemplateRenderer(_layoutsDir);

        var html = renderer.RenderPage("layouts/base.html", new PageModel
        {
            Site = new SiteModel { Name = "starter-test", Title = "Starter Test", BaseUrl = "", Language = "x\"><script>" },
            Page = new PageInfo { Title = "Page", Url = "/page/", Content = "" }
        });

        Assert.Contains("<html lang=\"x&quot;&gt;&lt;script&gt;\">", html, StringComparison.Ordinal);
        Assert.DoesNotContain("x\"><script>", html, StringComparison.Ordinal);

        var validHtml = renderer.RenderPage("layouts/base.html", new PageModel
        {
            Site = new SiteModel { Name = "starter-test", Title = "Starter Test", BaseUrl = "", Language = "zh-CN" },
            Page = new PageInfo { Title = "Page", Url = "/page/", Content = "" }
        });
        Assert.Contains("<html lang=\"zh-CN\">", validHtml, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("", "", "/assets/style.css", "/rss.xml")]
    [InlineData("/", "/", "/assets/style.css", "/rss.xml")]
    [InlineData("/repo", "/repo", "/repo/assets/style.css", "/repo/rss.xml")]
    [InlineData("/repo/zh-CN", "/repo", "/repo/assets/style.css", "/repo/zh-CN/rss.xml")]
    public void StarterBaseLayout_UsesAssetBaseUrlAndKeepsLanguageUrl(
        string baseUrl,
        string assetBaseUrl,
        string expectedStylesheet,
        string expectedFeed)
    {
        var renderer = new ScribanTemplateRenderer(_layoutsDir);
        var html = renderer.RenderPage("layouts/base.html", new PageModel
        {
            Site = new SiteModel
            {
                Name = "starter-test",
                Title = "Starter Test",
                BaseUrl = baseUrl,
                AssetBaseUrl = assetBaseUrl,
                Language = "zh-CN"
            },
            Page = new PageInfo { Title = "Page", Url = "/page/", Content = "" }
        });

        Assert.Contains($"href=\"{expectedStylesheet}\"", html, StringComparison.Ordinal);
        Assert.Contains($"href=\"{expectedFeed}\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("//assets/style.css", html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("zh-CN", "const siteLanguage = \"zh-CN\".toLowerCase();")]
    [InlineData("x\"</script>", "const siteLanguage = \"x\\\"\\u003C/script>\".toLowerCase();")]
    public void StarterSearchTemplate_FiltersRecordsBySiteLanguage(string language, string expectedDeclaration)
    {
        var renderer = new ScribanTemplateRenderer(_layoutsDir);

        var html = renderer.RenderPage("pages/search.html", new PageModel
        {
            Site = new SiteModel { Name = "starter-test", Title = "Starter Test", BaseUrl = "/repo", Language = language },
            Page = new PageInfo { Title = "Search", Url = "/search/", Content = "" }
        });

        Assert.Contains(expectedDeclaration, html, StringComparison.Ordinal);
        Assert.Contains(".filter(matchesSiteLanguage)", html, StringComparison.Ordinal);
        Assert.Contains("fetch('/repo/search.json')", html, StringComparison.Ordinal);
        Assert.DoesNotContain("siteLanguage = \"x\"</script>", html, StringComparison.Ordinal);
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

    private static string StarterThemePath(string resourceName) =>
        Path.Combine(FindRepositoryRoot(), "src", "Bukit-Core", "Bukit.Cli", "Resources", "StarterTheme", resourceName);

    private void CopyResource(string resourceName, string relativeTarget)
    {
        var source = StarterThemePath(resourceName);
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
