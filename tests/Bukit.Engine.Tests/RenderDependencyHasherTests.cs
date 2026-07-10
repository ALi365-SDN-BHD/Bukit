using System.Collections.Generic;
using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Incremental;
using Bukit.Rendering;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class RenderDependencyHasherTests
{
    private static readonly SiteModel s_emptySiteModel = new()
    {
        Name = "test",
        Title = "test",
        BaseUrl = "/",
        Language = "en"
    };

    private static AppConfig CreateBaseConfig()
    {
        return new AppConfig
        {
            Site = new SiteConfig
            {
                Name = "test",
                Title = "Test Site"
            },
            Content = TestContent.Markdown() with
            {
                Media = new MediaConfig { DownloadToLocal = false }
            },
            Build = new BuildConfig { Output = "dist" },
            Theme = new ThemeConfig { Layouts = "layouts" }
        };
    }

    [Fact]
    public void Compute_SameConfig_ProducesSameHash()
    {
        var config1 = CreateBaseConfig();
        var config2 = CreateBaseConfig();

        var hash1 = RenderDependencyHasher.Compute(config1, s_emptySiteModel);
        var hash2 = RenderDependencyHasher.Compute(config2, s_emptySiteModel);

        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void Compute_DifferentUrl_ProducesDifferentHash()
    {
        var config1 = CreateBaseConfig();
        var config2 = CreateBaseConfig() with
        {
            Site = CreateBaseConfig().Site with { Url = "https://other.com" }
        };

        var hash1 = RenderDependencyHasher.Compute(config1, s_emptySiteModel);
        var hash2 = RenderDependencyHasher.Compute(config2, s_emptySiteModel);

        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void Compute_UrlNull_DoesNotThrow()
    {
        var config = CreateBaseConfig();

        var hash = RenderDependencyHasher.Compute(config, s_emptySiteModel);

        Assert.NotNull(hash);
        Assert.NotEmpty(hash);
    }

    [Fact]
    public void Compute_DifferentLanguages_ProducesDifferentHash()
    {
        var config1 = CreateBaseConfig();
        var config2 = CreateBaseConfig() with
        {
            Site = CreateBaseConfig().Site with { Languages = new[] { "en", "zh", "fr" } }
        };

        var hash1 = RenderDependencyHasher.Compute(config1, s_emptySiteModel);
        var hash2 = RenderDependencyHasher.Compute(config2, s_emptySiteModel);

        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void Compute_LanguagesOrder_ProducesSameHash()
    {
        var config1 = CreateBaseConfig() with
        {
            Site = CreateBaseConfig().Site with { Languages = new[] { "en", "zh" } }
        };
        var config2 = CreateBaseConfig() with
        {
            Site = CreateBaseConfig().Site with { Languages = new[] { "zh", "en" } }
        };

        var hash1 = RenderDependencyHasher.Compute(config1, s_emptySiteModel);
        var hash2 = RenderDependencyHasher.Compute(config2, s_emptySiteModel);

        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void Compute_LanguagesNull_DoesNotThrow()
    {
        var config = CreateBaseConfig();

        var hash = RenderDependencyHasher.Compute(config, s_emptySiteModel);

        Assert.NotNull(hash);
        Assert.NotEmpty(hash);
    }

    [Fact]
    public void Compute_LanguagesEmpty_DoesNotThrow()
    {
        var config = CreateBaseConfig() with
        {
            Site = CreateBaseConfig().Site with { Languages = Array.Empty<string>() }
        };

        var hash = RenderDependencyHasher.Compute(config, s_emptySiteModel);

        Assert.NotNull(hash);
        Assert.NotEmpty(hash);
    }

    [Fact]
    public void Compute_DifferentDefaultLanguage_ProducesDifferentHash()
    {
        var config1 = CreateBaseConfig();
        var config2 = CreateBaseConfig() with
        {
            Site = CreateBaseConfig().Site with { DefaultLanguage = "zh" }
        };

        var hash1 = RenderDependencyHasher.Compute(config1, s_emptySiteModel);
        var hash2 = RenderDependencyHasher.Compute(config2, s_emptySiteModel);

        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void Compute_DefaultLanguageNull_DoesNotThrow()
    {
        var config = CreateBaseConfig();

        var hash = RenderDependencyHasher.Compute(config, s_emptySiteModel);

        Assert.NotNull(hash);
        Assert.NotEmpty(hash);
    }

    [Fact]
    public void Compute_DifferentSitemapMode_ProducesDifferentHash()
    {
        var config1 = CreateBaseConfig();
        var config2 = CreateBaseConfig() with
        {
            Site = CreateBaseConfig().Site with { SitemapMode = "merged" }
        };

        var hash1 = RenderDependencyHasher.Compute(config1, s_emptySiteModel);
        var hash2 = RenderDependencyHasher.Compute(config2, s_emptySiteModel);

        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void Compute_DifferentFeedMode_ProducesDifferentHash()
    {
        var config1 = CreateBaseConfig();
        var config2 = CreateBaseConfig() with
        {
            Site = CreateBaseConfig().Site with { Feed = CreateBaseConfig().Site.Feed with { Mode = "merged" } }
        };

        var hash1 = RenderDependencyHasher.Compute(config1, s_emptySiteModel);
        var hash2 = RenderDependencyHasher.Compute(config2, s_emptySiteModel);

        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void Compute_DifferentSearchMode_ProducesDifferentHash()
    {
        var config1 = CreateBaseConfig();
        var config2 = CreateBaseConfig() with
        {
            Site = CreateBaseConfig().Site with { Search = CreateBaseConfig().Site.Search with { Mode = "merged" } }
        };

        var hash1 = RenderDependencyHasher.Compute(config1, s_emptySiteModel);
        var hash2 = RenderDependencyHasher.Compute(config2, s_emptySiteModel);

        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void Compute_ExistingFieldsStillAffectHash()
    {
        var config1 = CreateBaseConfig();
        var config2 = CreateBaseConfig() with
        {
            Site = CreateBaseConfig().Site with { Title = "Changed Title" }
        };

        var hash1 = RenderDependencyHasher.Compute(config1, s_emptySiteModel);
        var hash2 = RenderDependencyHasher.Compute(config2, s_emptySiteModel);

        Assert.NotEqual(hash1, hash2);
    }

    [Theory]
    [InlineData("homeTitleTemplate")]
    [InlineData("pageTitleTemplate")]
    [InlineData("titleSeparator")]
    public void Compute_SeoDocumentTitleSettingsChange_ProducesDifferentHash(string setting)
    {
        var config1 = CreateBaseConfig();
        var changedSeo = setting switch
        {
            "homeTitleTemplate" => config1.Site.Seo with { HomeTitleTemplate = "{siteTitle} Home" },
            "pageTitleTemplate" => config1.Site.Seo with { PageTitleTemplate = "{pageTitle}{separator}{siteTitle}" },
            "titleSeparator" => config1.Site.Seo with { TitleSeparator = " - " },
            _ => throw new ArgumentOutOfRangeException(nameof(setting))
        };
        var config2 = config1 with
        {
            Site = config1.Site with { Seo = changedSeo }
        };

        var hash1 = RenderDependencyHasher.Compute(config1, s_emptySiteModel);
        var hash2 = RenderDependencyHasher.Compute(config2, s_emptySiteModel);

        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void Compute_DifferentCollectionPaginationEnabled_ProducesDifferentHash()
    {
        var baseConfig = CreateBaseConfig() with
        {
            Site = CreateBaseConfig().Site with
            {
                Collections = new Dictionary<string, CollectionConfig>(StringComparer.OrdinalIgnoreCase)
                {
                    ["blog"] = new CollectionConfig { Permalink = "/blog/{slug}/", Template = "pages/post.html" }
                }
            }
        };
        var config2 = baseConfig with
        {
            Site = baseConfig.Site with
            {
                Collections = new Dictionary<string, CollectionConfig>(StringComparer.OrdinalIgnoreCase)
                {
                    ["blog"] = new CollectionConfig
                    {
                        Permalink = "/blog/{slug}/",
                        Template = "pages/post.html",
                        Pagination = new CollectionPaginationConfig { Enabled = true }
                    }
                }
            }
        };
        Assert.NotEqual(RenderDependencyHasher.Compute(baseConfig, s_emptySiteModel), RenderDependencyHasher.Compute(config2, s_emptySiteModel));
    }

    [Fact]
    public void Compute_DifferentFilteredListPaginationConfig_ProducesDifferentHash()
    {
        var baseConfig = CreateBaseConfig() with
        {
            Site = CreateBaseConfig().Site with
            {
                Collections = new Dictionary<string, CollectionConfig>(StringComparer.OrdinalIgnoreCase)
                {
                    ["company"] = new CollectionConfig
                    {
                        Permalink = "/companies/{slug}/",
                        Template = "pages/company.html",
                        ListRoute = "/companies/",
                        FilteredLists = new[]
                        {
                            new FilteredListConfig
                            {
                                Field = "country",
                                Value = "Malaysia",
                                ListRoute = "/companies/malaysia/",
                                PageSize = 2,
                                UrlPattern = "page/{page}/",
                                EmptyBehavior = "render"
                            }
                        }
                    }
                }
            }
        };
        var config2 = baseConfig with
        {
            Site = baseConfig.Site with
            {
                Collections = new Dictionary<string, CollectionConfig>(StringComparer.OrdinalIgnoreCase)
                {
                    ["company"] = new CollectionConfig
                    {
                        Permalink = "/companies/{slug}/",
                        Template = "pages/company.html",
                        ListRoute = "/companies/",
                        FilteredLists = new[]
                        {
                            new FilteredListConfig
                            {
                                Field = "country",
                                Value = "Malaysia",
                                ListRoute = "/companies/malaysia/",
                                PageSize = 3,
                                UrlPattern = "p/{page}/",
                                EmptyBehavior = "skip"
                            }
                        }
                    }
                }
            }
        };

        Assert.NotEqual(RenderDependencyHasher.Compute(baseConfig, s_emptySiteModel), RenderDependencyHasher.Compute(config2, s_emptySiteModel));
    }

    [Fact]
    public void Compute_DifferentFilteredListOperatorConfig_ProducesDifferentHash()
    {
        var baseConfig = CreateBaseConfig() with
        {
            Site = CreateBaseConfig().Site with
            {
                Collections = new Dictionary<string, CollectionConfig>(StringComparer.OrdinalIgnoreCase)
                {
                    ["company"] = new CollectionConfig
                    {
                        Permalink = "/companies/{slug}/",
                        Template = "pages/company.html",
                        ListRoute = "/companies/",
                        FilteredLists = new[]
                        {
                            new FilteredListConfig
                            {
                                Field = "category",
                                Operator = "equals",
                                Value = "市场观察",
                                ListRoute = "/companies/market/"
                            }
                        }
                    }
                }
            }
        };
        var config2 = baseConfig with
        {
            Site = baseConfig.Site with
            {
                Collections = new Dictionary<string, CollectionConfig>(StringComparer.OrdinalIgnoreCase)
                {
                    ["company"] = new CollectionConfig
                    {
                        Permalink = "/companies/{slug}/",
                        Template = "pages/company.html",
                        ListRoute = "/companies/",
                        FilteredLists = new[]
                        {
                            new FilteredListConfig
                            {
                                Field = "category",
                                Operator = "in",
                                Values = new[] { "市场观察", "政策动态" },
                                ListRoute = "/companies/market/"
                            }
                        }
                    }
                }
            }
        };

        Assert.NotEqual(RenderDependencyHasher.Compute(baseConfig, s_emptySiteModel), RenderDependencyHasher.Compute(config2, s_emptySiteModel));
    }

    [Fact]
    public void Compute_DifferentCollectionOutputRss_ProducesDifferentHash()
    {
        var baseConfig = CreateBaseConfig() with
        {
            Site = CreateBaseConfig().Site with
            {
                Collections = new Dictionary<string, CollectionConfig>(StringComparer.OrdinalIgnoreCase)
                {
                    ["blog"] = new CollectionConfig
                    {
                        Permalink = "/blog/{slug}/",
                        Template = "pages/post.html",
                        Output = new CollectionOutputConfig { Rss = true }
                    }
                }
            }
        };
        var config2 = baseConfig with
        {
            Site = baseConfig.Site with
            {
                Collections = new Dictionary<string, CollectionConfig>(StringComparer.OrdinalIgnoreCase)
                {
                    ["blog"] = new CollectionConfig
                    {
                        Permalink = "/blog/{slug}/",
                        Template = "pages/post.html",
                        Output = new CollectionOutputConfig { Rss = false }
                    }
                }
            }
        };
        Assert.NotEqual(RenderDependencyHasher.Compute(baseConfig, s_emptySiteModel), RenderDependencyHasher.Compute(config2, s_emptySiteModel));
    }

    [Fact]
    public void Compute_DifferentTaxonomyPageSize_ProducesDifferentHash()
    {
        var baseConfig = CreateBaseConfig() with
        {
            Taxonomy = new TaxonomyConfig { Kinds = new[] { new TaxonomyKindConfig { Key = "tags" } }, PageSize = 10 }
        };
        var config2 = baseConfig with
        {
            Taxonomy = new TaxonomyConfig { Kinds = new[] { new TaxonomyKindConfig { Key = "tags" } }, PageSize = 20 }
        };
        Assert.NotEqual(RenderDependencyHasher.Compute(baseConfig, s_emptySiteModel), RenderDependencyHasher.Compute(config2, s_emptySiteModel));
    }

    [Fact]
    public void Compute_DifferentTaxonomyOutputMode_ProducesDifferentHash()
    {
        var baseConfig = CreateBaseConfig() with
        {
            Taxonomy = new TaxonomyConfig { Kinds = new[] { new TaxonomyKindConfig { Key = "tags" } }, OutputMode = "both" }
        };
        var config2 = baseConfig with
        {
            Taxonomy = new TaxonomyConfig { Kinds = new[] { new TaxonomyKindConfig { Key = "tags" } }, OutputMode = "terms_only" }
        };
        Assert.NotEqual(RenderDependencyHasher.Compute(baseConfig, s_emptySiteModel), RenderDependencyHasher.Compute(config2, s_emptySiteModel));
    }

    [Fact]
    public void Compute_DifferentTaxonomyKindTemplates_ProducesDifferentHash()
    {
        var baseConfig = CreateBaseConfig() with
        {
            Taxonomy = new TaxonomyConfig
            {
                Kinds = new[] { new TaxonomyKindConfig { Key = "tags", Template = "pages/tag.html" } }
            }
        };
        var config2 = baseConfig with
        {
            Taxonomy = new TaxonomyConfig
            {
                Kinds = new[] { new TaxonomyKindConfig { Key = "tags", Template = "pages/tag-alt.html" } }
            }
        };
        Assert.NotEqual(RenderDependencyHasher.Compute(baseConfig, s_emptySiteModel), RenderDependencyHasher.Compute(config2, s_emptySiteModel));
    }

    [Fact]
    public void Compute_DifferentTaxonomyKindRoutePrefix_ProducesDifferentHash()
    {
        var baseConfig = CreateBaseConfig() with
        {
            Taxonomy = new TaxonomyConfig
            {
                Kinds = new[] { new TaxonomyKindConfig { Key = "categories", Kind = "category" } }
            }
        };
        var config2 = baseConfig with
        {
            Taxonomy = new TaxonomyConfig
            {
                Kinds = new[] { new TaxonomyKindConfig { Key = "categories", Kind = "category", RoutePrefix = "/insights/category" } }
            }
        };
        Assert.NotEqual(RenderDependencyHasher.Compute(baseConfig, s_emptySiteModel), RenderDependencyHasher.Compute(config2, s_emptySiteModel));
    }
}
