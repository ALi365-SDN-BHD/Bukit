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

    private static AnalyticsProviderConfig CreateAnalyticsProvider() => new()
    {
        Type = "google-analytics",
        MeasurementId = "G-ABC123"
    };

    [Fact]
    public void Compute_SameConfig_ProducesSameHash()
    {
        var config1 = CreateBaseConfig();
        var config2 = CreateBaseConfig();

        var hash1 = RenderDependencyHasher.Compute(config1, s_emptySiteModel);
        var hash2 = RenderDependencyHasher.Compute(config2, s_emptySiteModel);

        Assert.Equal(hash1, hash2);
    }

    [Theory]
    [InlineData("enabled")]
    [InlineData("productionOnly")]
    public void Compute_AnalyticsSwitchChange_ProducesDifferentHash(string setting)
    {
        var baseConfig = CreateBaseConfig() with
        {
            Site = CreateBaseConfig().Site with
            {
                Analytics = new AnalyticsConfig
                {
                    Enabled = true,
                    ProductionOnly = true,
                    Providers = [CreateAnalyticsProvider()]
                }
            }
        };
        var changedAnalytics = setting switch
        {
            "enabled" => baseConfig.Site.Analytics with { Enabled = false },
            "productionOnly" => baseConfig.Site.Analytics with { ProductionOnly = false },
            _ => throw new ArgumentOutOfRangeException(nameof(setting))
        };
        var changed = baseConfig with
        {
            Site = baseConfig.Site with { Analytics = changedAnalytics }
        };

        Assert.NotEqual(
            RenderDependencyHasher.Compute(baseConfig, s_emptySiteModel),
            RenderDependencyHasher.Compute(changed, s_emptySiteModel));
    }

    [Fact]
    public void Compute_AnalyticsResolvedProviderOptionsChange_ProducesDifferentHash()
    {
        var providerPairs = new (AnalyticsProviderConfig Before, AnalyticsProviderConfig After)[]
        {
            (
                new AnalyticsProviderConfig { Type = "google-analytics", MeasurementId = "G-ABC123" },
                new AnalyticsProviderConfig { Type = "google-analytics", MeasurementId = "G-XYZ789" }),
            (
                new AnalyticsProviderConfig { Type = "google-tag-manager", ContainerId = "GTM-ABC123" },
                new AnalyticsProviderConfig { Type = "google-tag-manager", ContainerId = "GTM-XYZ789" }),
            (
                new AnalyticsProviderConfig
                {
                    Type = "plausible", Domain = "example.com",
                    ScriptUrl = "https://plausible.io/js/script.js"
                },
                new AnalyticsProviderConfig
                {
                    Type = "plausible", Domain = "changed.example.com",
                    ScriptUrl = "https://stats.example.com/js/script.js"
                }),
            (
                new AnalyticsProviderConfig
                {
                    Type = "umami", WebsiteId = "00000000-0000-0000-0000-000000000001",
                    ScriptUrl = "https://analytics.example.com/script.js"
                },
                new AnalyticsProviderConfig
                {
                    Type = "umami", WebsiteId = "00000000-0000-0000-0000-000000000002",
                    ScriptUrl = "https://changed.example.com/script.js"
                })
        };

        foreach (var (before, after) in providerPairs)
        {
            var baseConfig = CreateBaseConfig() with
            {
                Site = CreateBaseConfig().Site with
                {
                    Analytics = new AnalyticsConfig { Providers = [before] }
                }
            };
            var changed = baseConfig with
            {
                Site = baseConfig.Site with
                {
                    Analytics = baseConfig.Site.Analytics with { Providers = [after] }
                }
            };

            Assert.NotEqual(
                RenderDependencyHasher.Compute(baseConfig, s_emptySiteModel),
                RenderDependencyHasher.Compute(changed, s_emptySiteModel));
        }
    }

    [Fact]
    public void Compute_AnalyticsProviderOrderChange_ProducesDifferentHash()
    {
        var first = CreateAnalyticsProvider();
        var second = new AnalyticsProviderConfig
        {
            Type = "plausible",
            Domain = "plausible.example.com",
            ScriptUrl = "https://plausible.io/js/script.js"
        };
        var baseConfig = CreateBaseConfig() with
        {
            Site = CreateBaseConfig().Site with
            {
                Analytics = new AnalyticsConfig { Providers = [first, second] }
            }
        };
        var reordered = baseConfig with
        {
            Site = baseConfig.Site with
            {
                Analytics = baseConfig.Site.Analytics with { Providers = [second, first] }
            }
        };

        Assert.NotEqual(
            RenderDependencyHasher.Compute(baseConfig, s_emptySiteModel),
            RenderDependencyHasher.Compute(reordered, s_emptySiteModel));
    }

    [Fact]
    public void Compute_AnalyticsPluginEffectiveToggleChange_ProducesDifferentHash()
    {
        var baseConfig = CreateBaseConfig();
        var explicitlyDisabled = baseConfig with
        {
            Site = baseConfig.Site with
            {
                Plugins = new Dictionary<string, PluginToggleConfig>(StringComparer.OrdinalIgnoreCase)
                {
                    ["analytics"] = new() { Enabled = false }
                }
            }
        };

        Assert.NotEqual(
            RenderDependencyHasher.Compute(baseConfig, s_emptySiteModel),
            RenderDependencyHasher.Compute(explicitlyDisabled, s_emptySiteModel));
    }

    [Fact]
    public void Compute_AnalyticsPluginMissingAndExplicitlyEnabled_AreStableEquivalent()
    {
        var baseConfig = CreateBaseConfig();
        var explicitlyEnabled = baseConfig with
        {
            Site = baseConfig.Site with
            {
                Plugins = new Dictionary<string, PluginToggleConfig>(StringComparer.OrdinalIgnoreCase)
                {
                    ["analytics"] = new() { Enabled = true }
                }
            }
        };

        Assert.Equal(
            RenderDependencyHasher.Compute(baseConfig, s_emptySiteModel),
            RenderDependencyHasher.Compute(explicitlyEnabled, s_emptySiteModel));
    }

    [Fact]
    public void Compute_ExecutionModeChange_ProducesDifferentHash()
    {
        var config = CreateBaseConfig();

        Assert.NotEqual(
            RenderDependencyHasher.Compute(config, s_emptySiteModel, BuildExecutionMode.Production),
            RenderDependencyHasher.Compute(config, s_emptySiteModel, BuildExecutionMode.Development));
    }

    [Fact]
    public void Compute_EquivalentNormalizedAnalyticsConfig_ProducesSameHash()
    {
        var unicode = CreateBaseConfig() with
        {
            Site = CreateBaseConfig().Site with
            {
                Analytics = new AnalyticsConfig
                {
                    Providers =
                    [
                        new AnalyticsProviderConfig
                        {
                            Type = "plausible",
                            Domain = "B\u00dcCHER.Example",
                            ScriptUrl = "https://plausible.io/js/script.js"
                        },
                        new AnalyticsProviderConfig
                        {
                            Type = "umami",
                            WebsiteId = "AAAAAAAA-BBBB-CCCC-DDDD-EEEEEEEEEEEE",
                            ScriptUrl = "https://analytics.example.com/script.js"
                        }
                    ]
                }
            }
        };
        var normalized = unicode with
        {
            Site = unicode.Site with
            {
                Analytics = unicode.Site.Analytics with
                {
                    Providers =
                    [
                        new AnalyticsProviderConfig
                        {
                            Type = "plausible",
                            Domain = "xn--bcher-kva.example",
                            ScriptUrl = "https://plausible.io/js/script.js"
                        },
                        new AnalyticsProviderConfig
                        {
                            Type = "umami",
                            WebsiteId = "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
                            ScriptUrl = "https://analytics.example.com/script.js"
                        }
                    ]
                }
            }
        };

        Assert.Equal(
            RenderDependencyHasher.Compute(unicode, s_emptySiteModel),
            RenderDependencyHasher.Compute(normalized, s_emptySiteModel));
    }

    [Fact]
    public void Compute_PlausibleOmittedAndExplicitDefaultScriptUrl_ProduceSameHash()
    {
        var omitted = CreateBaseConfig() with
        {
            Site = CreateBaseConfig().Site with
            {
                Analytics = new AnalyticsConfig
                {
                    Providers =
                    [
                        new AnalyticsProviderConfig { Type = "plausible", Domain = "example.com" }
                    ]
                }
            }
        };
        var explicitDefault = omitted with
        {
            Site = omitted.Site with
            {
                Analytics = omitted.Site.Analytics with
                {
                    Providers =
                    [
                        new AnalyticsProviderConfig
                        {
                            Type = "plausible",
                            Domain = "example.com",
                            ScriptUrl = "https://plausible.io/js/script.js"
                        }
                    ]
                }
            }
        };

        Assert.Equal(
            RenderDependencyHasher.Compute(omitted, s_emptySiteModel),
            RenderDependencyHasher.Compute(explicitDefault, s_emptySiteModel));
    }

    [Fact]
    public void Compute_DifferentDataIndexValue_ProducesDifferentHash()
    {
        static SiteModel CreateSite(string email) => new()
        {
            Name = "test",
            Title = "test",
            BaseUrl = "/",
            Language = "en",
            DataIndex = new Dictionary<string, object>
            {
                ["settings"] = new Dictionary<string, object>
                {
                    ["contact"] = new Dictionary<string, object>
                    {
                        ["email"] = email
                    }
                }
            }
        };
        var config = CreateBaseConfig();

        var hash1 = RenderDependencyHasher.Compute(config, CreateSite("a@example.com"));
        var hash2 = RenderDependencyHasher.Compute(config, CreateSite("b@example.com"));

        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void Compute_DifferentBuildYear_ProducesDifferentHash()
    {
        static SiteModel CreateSite(int buildYear) => new()
        {
            Name = "test",
            Title = "test",
            BaseUrl = "/",
            Language = "en",
            BuildYear = buildYear
        };
        var config = CreateBaseConfig();

        var hash1 = RenderDependencyHasher.Compute(config, CreateSite(2025));
        var hash2 = RenderDependencyHasher.Compute(config, CreateSite(2026));

        Assert.NotEqual(hash1, hash2);
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
