using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Engine.Plugins;
using Bukit.Rendering;
using Bukit.Routing;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Shared;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class SiteEngineHelperExtendedTests
{
    private static ContentDocument CreateDocument(string id, string title, string slug)
    {
        return ContentDocument.Create(id, title, slug, DateTimeOffset.UtcNow, null, null);
    }

    private static ContentDocument CreateTaxonomyDocument(string id, string title, string slug, string language, string category)
    {
        return ContentDocument.Create(
            id,
            title,
            slug,
            DateTimeOffset.Parse("2026-06-05T00:00:00Z"),
            null,
            ContentFieldReader.ToFieldMap(new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["language"] = language,
                ["collection"] = "article",
                ["categories"] = new[] { category }
            }));
    }

    private static AppConfig CreateTestConfig()
    {
        return new AppConfig
        {
            Site = new SiteConfig { Name = "test", Title = "Test", BaseUrl = "/", Url = "https://example.com" },
            Content = TestContent.Markdown(),
            Taxonomy = new TaxonomyConfig()
        };
    }

    [Fact]
    public void BuildSeoAlternates_WithMultipleLanguages_ReturnsAlternates()
    {
        var config = new AppConfig
        {
            Site = new SiteConfig
            {
                Name = "test",
                Title = "Test",
                BaseUrl = "/",
                Url = "https://example.com",
                Collections = new Dictionary<string, CollectionConfig>(StringComparer.OrdinalIgnoreCase)
                {
                    ["article"] = new()
                    {
                        Permalink = "/articles/{slug}/",
                        Template = "pages/article.html"
                    }
                }
            },
            Content = TestContent.Markdown()
        };
        var documents = new List<ContentDocument>
        {
            ContentDocument.Create("1", "Post One", "post-one", DateTimeOffset.UtcNow, null,
                ContentFieldReader.ToFieldMap(new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase) { ["collection"] = "article" })),
        };
        var languages = new List<string> { "en", "zh" };
        var defaultLanguage = "en";
        var rootBaseUrl = "/";

        var result = SeoAlternatesService.BuildSeoAlternates(config, documents, languages, defaultLanguage, rootBaseUrl);

        Assert.NotNull(result);
    }

    [Fact]
    public void BuildSeoAlternates_WithFilteredListPagination_UsesRouteGraphRoutes()
    {
        var config = new AppConfig
        {
            Site = new SiteConfig
            {
                Name = "test",
                Title = "Test",
                BaseUrl = "/",
                Url = "https://example.com",
                Collections = new Dictionary<string, CollectionConfig>(StringComparer.OrdinalIgnoreCase)
                {
                    ["company"] = new()
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
                                PageSize = 2,
                                UrlPattern = "page/{page}/"
                            }
                        }
                    }
                }
            },
            Content = TestContent.Markdown()
        };
        var documents = new List<ContentDocument>();
        foreach (var language in new[] { "en", "zh" })
        {
            for (var i = 1; i <= 3; i++)
            {
                documents.Add(ContentDocument.Create(
                    $"{language}-company-{i}",
                    $"{language} Company {i}",
                    $"{language}-company-{i}",
                    new DateTimeOffset(2026, 1, i, 0, 0, 0, TimeSpan.Zero),
                    null,
                    ContentFieldReader.ToFieldMap(new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["collection"] = "company",
                        ["country"] = "Malaysia",
                        ["language"] = language
                    })));
            }
        }

        var result = SeoAlternatesService.BuildSeoAlternates(config, documents, new[] { "en", "zh" }, "en", "/");

        var alternates = result["route:/companies/malaysia/page/2/"];
        Assert.Contains(alternates, alternate => alternate.Hreflang == "x-default" && alternate.Href == "https://example.com/en/companies/malaysia/page/2/");
        Assert.Contains(alternates, alternate => alternate.Hreflang == "en" && alternate.Href == "https://example.com/en/companies/malaysia/page/2/");
        Assert.Contains(alternates, alternate => alternate.Hreflang == "zh" && alternate.Href == "https://example.com/zh/companies/malaysia/page/2/");
    }

    [Fact]
    public void BuildSeoAlternates_WithTaxonomyRoutePrefix_DoesNotInventMissingLanguageRoutes()
    {
        var rootDir = Path.Combine(Path.GetTempPath(), "bukit-seo-alternates-taxonomy-" + Guid.NewGuid().ToString("N"));
        var layoutsDir = Path.Combine(rootDir, "layouts");
        Directory.CreateDirectory(Path.Combine(layoutsDir, "pages"));
        File.WriteAllText(Path.Combine(layoutsDir, "pages", "taxonomy-index.html"), "{{ page.title }}");
        File.WriteAllText(Path.Combine(layoutsDir, "pages", "taxonomy-term.html"), "{{ page.title }}");

        try
        {
            var config = new AppConfig
            {
                Site = new SiteConfig
                {
                    Name = "test",
                    Title = "Test",
                    BaseUrl = "/",
                    Url = "https://example.com",
                    Collections = new Dictionary<string, CollectionConfig>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["article"] = new()
                        {
                            Permalink = "/insights/{slug}/",
                            Template = "pages/article.html"
                        }
                    }
                },
                Content = TestContent.Markdown(),
                Taxonomy = new TaxonomyConfig
                {
                    OutputMode = "pages",
                    IndexEnabled = true,
                    Kinds = new List<TaxonomyKindConfig>
                    {
                        new()
                        {
                            Key = "categories",
                            Kind = "category",
                            Title = "Categories",
                            SingularTitlePrefix = "Category",
                            RoutePrefix = "/insights/category",
                            IndexTemplate = "pages/taxonomy-index.html",
                            TermTemplate = "pages/taxonomy-term.html"
                        }
                    }
                }
            };
            var documents = new List<ContentDocument>
            {
                CreateTaxonomyDocument("en-shared", "EN Shared", "en-shared", "en", "Shared"),
                CreateTaxonomyDocument("zh-shared", "ZH Shared", "zh-shared", "zh", "Shared"),
                CreateTaxonomyDocument("en-market", "EN Market", "en-market", "en", "Market")
            };

            var result = SeoAlternatesService.BuildSeoAlternates(
                config,
                documents,
                new[] { "en", "zh" },
                defaultLanguage: "en",
                rootBaseUrl: "/",
                templateResolver: null,
                rootDir: rootDir,
                layoutsDir: layoutsDir);

            var sharedAlternates = result["route:/insights/category/shared/"];
            Assert.Contains(sharedAlternates, alternate => alternate.Hreflang == "x-default" && alternate.Href == "https://example.com/en/insights/category/shared/");
            Assert.Contains(sharedAlternates, alternate => alternate.Hreflang == "en" && alternate.Href == "https://example.com/en/insights/category/shared/");
            Assert.Contains(sharedAlternates, alternate => alternate.Hreflang == "zh" && alternate.Href == "https://example.com/zh/insights/category/shared/");
            Assert.DoesNotContain("route:/insights/category/market/", result.Keys);
        }
        finally
        {
            if (Directory.Exists(rootDir))
            {
                Directory.Delete(rootDir, recursive: true);
            }
        }
    }

    [Fact]
    public void BuildSeoAlternates_WithEmptyLanguages_ReturnsEmpty()
    {
        var config = CreateTestConfig();
        var documents = new List<ContentDocument>
        {
            CreateDocument("1", "Post One", "post-one"),
        };
        var languages = Array.Empty<string>();
        var defaultLanguage = "en";
        var rootBaseUrl = "/";

        var result = SeoAlternatesService.BuildSeoAlternates(config, documents, languages, defaultLanguage, rootBaseUrl);

        Assert.NotNull(result);
        Assert.Empty(result!);
    }

    [Fact]
    public void AddVariantRouteAlternates_WithMissingPrecomputedRoute_DoesNotInventLanguageUrls()
    {
        var config = new AppConfig
        {
            Site = new SiteConfig
            {
                Name = "test",
                Title = "Test",
                BaseUrl = "/",
                Url = "https://example.com",
                Languages = new[] { "en", "zh" }
            },
            Content = TestContent.Markdown()
        };
        var graph = ListRouteGraph.Create(new[]
        {
            new ListRoutePlan
            {
                RouteId = "filter:company:country:malaysia:2",
                Kind = ListRouteKind.FilteredListPage,
                Url = "/companies/malaysia/page-two/",
                OutputPath = "companies/malaysia/page-two/index.html",
                Template = "pages/company-list.html",
                Collection = "company",
                PageNumber = 2,
                PageSize = 2,
                TotalItems = 3,
                CanonicalUrl = "/companies/malaysia/p/2/",
                PrevUrl = "/companies/malaysia/"
            }
        });

        var result = SeoAlternatesService.AddVariantRouteAlternates(
            config,
            new Dictionary<string, IReadOnlyList<SeoAlternateModel>>(StringComparer.Ordinal),
            graph,
            rootBaseUrl: "/",
            defaultLanguage: "en");

        Assert.Empty(result);
    }

    [Fact]
    public void AddVariantRouteAlternates_WithNoVariants_ReturnsExisting()
    {
        var config = CreateTestConfig();
        var existing = new Dictionary<string, IReadOnlyList<SeoAlternateModel>>(StringComparer.Ordinal)
        {
            ["/test"] = new List<SeoAlternateModel> { new("en", "https://example.com/test") }
        };
        var rootBaseUrl = "/";
        var defaultLanguage = "en";

        var result = SeoAlternatesService.AddVariantRouteAlternates(config, existing, ListRouteGraph.Empty, rootBaseUrl, defaultLanguage);

        Assert.NotNull(result);
        Assert.Same(existing, result);
    }

    [Fact]
    public void BuildListOutputPath_WithValidRoute_ReturnsPath()
    {
        var listRoute = "/posts";

        var result = RoutePathBuilder.BuildOutputPathFromUrl(listRoute);

        Assert.NotNull(result);
        Assert.Contains("index.html", result, StringComparison.Ordinal);
    }

    [Fact]
    public void WriteRobotsTxtIfRequested_WhenEnabled_WritesFile()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "bukit_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var config = new AppConfig
            {
                Site = new SiteConfig
                {
                    Name = "Test",
                    Title = "Test",
                    BaseUrl = "/",
                    Url = "https://example.com",
                    Seo = new SeoConfig
                    {
                        RobotsTxt = new SeoRobotsTxtConfig { Enabled = true }
                    }
                },
                Content = TestContent.Markdown()
            };
            var seoEntries = new Dictionary<string, SeoIndexEntry>();

            RobotsTxtWriter.WriteIfRequested(config, tempDir, "/", seoEntries);

            var robotsPath = Path.Combine(tempDir, "robots.txt");
            Assert.True(File.Exists(robotsPath));
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
    public void WriteRobotsTxtIfRequested_WhenDisabled_DoesNotWriteFile()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "bukit_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var config = new AppConfig
            {
                Site = new SiteConfig
                {
                    Name = "Test",
                    Title = "Test",
                    BaseUrl = "/",
                    Seo = new SeoConfig
                    {
                        RobotsTxt = new SeoRobotsTxtConfig { Enabled = false }
                    }
                },
                Content = TestContent.Markdown()
            };
            var seoEntries = new Dictionary<string, SeoIndexEntry>();

            RobotsTxtWriter.WriteIfRequested(config, tempDir, "/", seoEntries);

            var robotsPath = Path.Combine(tempDir, "robots.txt");
            Assert.False(File.Exists(robotsPath));
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
    public void GetSeoAlternates_WithExistingKey_ReturnsAlternates()
    {
        var alternates = new Dictionary<string, IReadOnlyList<SeoAlternateModel>>(StringComparer.Ordinal)
        {
            ["/test"] = new List<SeoAlternateModel>
            {
                new("en", "https://example.com/en/test"),
                new("zh", "https://example.com/zh/test"),
            }
        };
        var key = "/test";

        var result = SeoPipeline.GetSeoAlternates(alternates, key);

        Assert.NotNull(result);
        Assert.Equal(2, result!.Count);
    }

    [Fact]
    public void GetSeoAlternates_WithMissingKey_ReturnsNull()
    {
        var alternates = new Dictionary<string, IReadOnlyList<SeoAlternateModel>>(StringComparer.Ordinal)
        {
            ["/test"] = new List<SeoAlternateModel> { new("en", "https://example.com/test") }
        };
        var key = "/nonexistent";

        var result = SeoPipeline.GetSeoAlternates(alternates, key);

        Assert.Null(result);
    }

    [Fact]
    public void NormalizeSeoPageSize_WithZero_Returns10()
    {
        var result = SeoAlternatesService.NormalizePageSize(0);

        Assert.Equal(10, result);
    }

    [Fact]
    public void NormalizeSeoPageSize_WithNegative_Returns10()
    {
        var result = SeoAlternatesService.NormalizePageSize(-5);

        Assert.Equal(10, result);
    }

    [Fact]
    public void NormalizeSeoPageSize_WithPositive_ReturnsSame()
    {
        var result = SeoAlternatesService.NormalizePageSize(20);

        Assert.Equal(20, result);
    }

    [Fact]
    public void SlugifySeoSegment_WithSimpleText_ReturnsSlugified()
    {
        var result = SlugHelper.Slugify("Hello World");

        Assert.Equal("hello-world", result);
    }

    [Fact]
    public void SlugifySeoSegment_WithSpecialCharacters_ReturnsCleanSlug()
    {
        var result = SlugHelper.Slugify("C# & .NET!");

        Assert.DoesNotContain("#", result, StringComparison.Ordinal);
        Assert.DoesNotContain("!", result, StringComparison.Ordinal);
    }
}
