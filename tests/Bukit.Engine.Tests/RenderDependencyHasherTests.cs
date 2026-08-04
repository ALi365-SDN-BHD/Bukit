using System.Collections.Generic;
using System.Globalization;
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

    private static AppConfig CreateRepresentativeGoldenConfig()
    {
        var config = CreateBaseConfig();
        return config with
        {
            Site = config.Site with
            {
                Description = "Golden description",
                BaseUrl = "/docs/",
                Language = "en",
                Languages = ["zh-CN", "en"],
                DefaultLanguage = "en",
                Url = "https://example.com",
                SitemapMode = "merged",
                Feed = config.Site.Feed with { Mode = "merged" },
                Search = config.Site.Search with { Mode = "merged" },
                Analytics = new AnalyticsConfig
                {
                    Enabled = true,
                    ProductionOnly = false,
                    Providers = [CreateAnalyticsProvider()]
                },
                Seo = config.Site.Seo with
                {
                    RenderMode = "inject",
                    HomeTitleTemplate = "{siteTitle} Home",
                    PageTitleTemplate = "{pageTitle} - {siteTitle}",
                    TitleSeparator = " - ",
                    DefaultImage = "/cover.png",
                    TwitterSite = "@bukit"
                },
                Collections = new Dictionary<string, CollectionConfig>
                {
                    ["posts"] = new()
                    {
                        Permalink = "/posts/{slug}/",
                        Template = "pages/post.html",
                        ListRoute = "/posts/",
                        ListTitle = "Posts",
                        ListDescription = "All posts",
                        ListTemplate = "pages/posts.html",
                        SchemaFailMode = "warn",
                        Pagination = new CollectionPaginationConfig
                        {
                            Enabled = true,
                            PageSize = 5,
                            UrlPattern = "page/{page}/",
                            FirstPageUsesListRoute = true
                        },
                        Output = new CollectionOutputConfig
                        {
                            Rss = true,
                            Sitemap = true,
                            Archive = true,
                            FeedPath = "posts.xml",
                            FeedTitle = "Posts feed",
                            FeedDescription = "Recent posts",
                            ArchiveDetail = new ArchiveDetailConfig
                            {
                                Depth = "monthly",
                                Template = "pages/archive.html",
                                RoutePrefix = "/archive/"
                            }
                        },
                        FilteredLists =
                        [
                            new FilteredListConfig
                            {
                                Field = "category",
                                Operator = "in",
                                Values = ["engineering", "news"],
                                ListRoute = "/posts/featured/",
                                Title = "Featured",
                                Description = "Featured posts",
                                ListTemplate = "pages/featured.html",
                                PageSize = 3,
                                UrlPattern = "p/{page}/",
                                EmptyBehavior = "skip"
                            }
                        ]
                    }
                },
                Plugins = new Dictionary<string, PluginToggleConfig>
                {
                    ["analytics"] = new() { Enabled = true },
                    ["search"] = new() { Enabled = false }
                }
            },
            Content = config.Content with
            {
                ModelSchema = new ContentModelSchemaConfig
                {
                    FieldScopes = new Dictionary<string, IReadOnlyList<CustomFieldDefinitionConfig>>
                    {
                        ["posts"] =
                        [
                            new CustomFieldDefinitionConfig
                            {
                                Name = "rating",
                                FieldType = "number",
                                Label = "Rating",
                                Format = "0.0",
                                Enum = ["1", "2"],
                                Min = 1,
                                Max = 5,
                                Required = true,
                                Default = 3
                            }
                        ]
                    }
                },
                RouteMetadata = new RouteMetadataConfig
                {
                    Source = "page_meta",
                    RouteField = "path",
                    TitleField = "heading",
                    SummaryField = "dek",
                    SeoTitleField = "meta_title",
                    SeoDescriptionField = "meta_description",
                    RequiredRoutes = ["/posts/", "/"]
                }
            },
            Build = config.Build with { ListPageContentMode = "full" },
            Theme = config.Theme with
            {
                Params = new Dictionary<string, object> { ["accent"] = "blue" },
                Shortcodes = new Dictionary<string, string> { ["notice"] = "shortcodes/notice.html" },
                Components = new Dictionary<string, ComponentDefinition>
                {
                    ["card"] = new()
                    {
                        Template = "components/card.html",
                        Props = new Dictionary<string, string> { ["tone"] = "info" }
                    }
                },
                ComponentValidation = "strict"
            },
            Taxonomy = new TaxonomyConfig
            {
                OutputMode = "both",
                PageSize = 12,
                IndexEnabled = true,
                PinField = "featured",
                PinOrderField = "rank",
                ItemFields = ["categories", "tags"],
                PinFieldBySource = new Dictionary<string, string> { ["posts"] = "pinned" },
                PinOrderFieldBySource = new Dictionary<string, string> { ["posts"] = "position" },
                Kinds =
                [
                    new TaxonomyKindConfig
                    {
                        Key = "tags",
                        Kind = "tag",
                        Title = "Tags",
                        Description = "Post tags",
                        SingularTitlePrefix = "Tag",
                        Template = "pages/tag.html",
                        IndexTemplate = "pages/tags.html",
                        TermTemplate = "pages/tag-term.html",
                        IndexEnabled = true,
                        Hierarchical = false,
                        RoutePrefix = "/tags/"
                    }
                ]
            }
        };
    }

    private static SiteModel CreateRepresentativeGoldenSiteModel() => new()
    {
        Name = "golden",
        Title = "Golden",
        BaseUrl = "/docs/",
        Language = "en",
        BuildYear = 2026,
        Modules = new Dictionary<string, IReadOnlyList<ModuleInfo>>
        {
            ["posts"] =
            [
                new ModuleInfo
                {
                    Id = "post-b",
                    Title = "Post B",
                    Slug = "post-b",
                    Content = "B"
                },
                new ModuleInfo
                {
                    Id = "post-a",
                    Title = "Post A",
                    Slug = "post-a",
                    Content = "A"
                }
            ],
            ["page_meta"] =
            [
                new ModuleInfo
                {
                    Id = "metadata",
                    Title = "Metadata",
                    Slug = "metadata",
                    Content = "reserved"
                }
            ]
        },
        Data = new Dictionary<string, object>
        {
            ["settings"] = new Dictionary<string, object> { ["region"] = "apac" },
            ["page_meta"] = "reserved"
        },
        DataIndex = new Dictionary<string, object>
        {
            ["settings"] = new Dictionary<string, object>
            {
                ["contact"] = new Dictionary<string, object> { ["email"] = "hello@example.com" }
            },
            ["page_meta"] = "reserved"
        }
    };

    [Fact]
    public void ContributorPlan_UsesStableOrder()
    {
        Assert.Equal(
            new[]
            {
                "site-identity-and-modes",
                "analytics",
                "seo",
                "theme-and-template-model",
                "collections-and-field-scopes",
                "taxonomy",
                "route-metadata-configuration",
                "non-analytics-plugin-enablement",
                "site-model-data"
            },
            RenderDependencyContributorPlan.Contributors.Select(contributor => contributor.Name));
    }

    [Fact]
    public void Compute_BaseConfiguration_MatchesGoldenHash()
    {
        Assert.Equal(
            "364a700bfde3ca7844620b94e3c3f9e13a372b406a981f669833978344dc2744",
            RenderDependencyHasher.Compute(CreateBaseConfig(), s_emptySiteModel));
    }

    [Fact]
    public void Compute_RepresentativeConfiguration_MatchesGoldenHash()
    {
        // Golden hash for the canonical framed/type-tagged render dependency encoding.
        Assert.Equal(
            "8358a536194c50101e7d121da7606458d83ca32f2d62072c74868fe38cadddf2",
            RenderDependencyHasher.Compute(
                CreateRepresentativeGoldenConfig(),
                CreateRepresentativeGoldenSiteModel(),
                BuildExecutionMode.Development,
                analyticsRendererContractVersion: "golden-contract-v1"));
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
                    SnippetMode = "legacy",
                    ScriptUrl = "https://plausible.io/js/script.js"
                },
                new AnalyticsProviderConfig
                {
                    Type = "plausible", Domain = "changed.example.com",
                    SnippetMode = "legacy",
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
            SnippetMode = "legacy",
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
    public void Compute_AnalyticsRendererContractVersionChange_ProducesDifferentHash()
    {
        var config = CreateBaseConfig() with
        {
            Site = CreateBaseConfig().Site with
            {
                Analytics = new AnalyticsConfig { Providers = [CreateAnalyticsProvider()] }
            }
        };

        var current = RenderDependencyHasher.Compute(config, s_emptySiteModel);
        var version1 = RenderDependencyHasher.Compute(
            config,
            s_emptySiteModel,
            analyticsRendererContractVersion: "1");
        var version2 = RenderDependencyHasher.Compute(
            config,
            s_emptySiteModel,
            analyticsRendererContractVersion: "2");
        var version3 = RenderDependencyHasher.Compute(
            config,
            s_emptySiteModel,
            analyticsRendererContractVersion: "3");
        var version4 = RenderDependencyHasher.Compute(
            config,
            s_emptySiteModel,
            analyticsRendererContractVersion: "4");
        var version5 = RenderDependencyHasher.Compute(
            config,
            s_emptySiteModel,
            analyticsRendererContractVersion: "5");

        Assert.NotEqual(version1, version2);
        Assert.NotEqual(version2, version3);
        Assert.NotEqual(version3, version4);
        Assert.NotEqual(version4, version5);
        Assert.Equal(version5, current);
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
    public void Compute_AnalyticsPluginDisabled_IgnoresInactiveAnalyticsSettingsAndExecutionMode()
    {
        var disabled = CreateBaseConfig() with
        {
            Site = CreateBaseConfig().Site with
            {
                Plugins = new Dictionary<string, PluginToggleConfig>(StringComparer.OrdinalIgnoreCase)
                {
                    ["analytics"] = new() { Enabled = false }
                },
                Analytics = new AnalyticsConfig
                {
                    Enabled = true,
                    ProductionOnly = true,
                    Providers = [CreateAnalyticsProvider()]
                }
            }
        };
        var inactiveSettingsChanged = disabled with
        {
            Site = disabled.Site with
            {
                Analytics = new AnalyticsConfig
                {
                    Enabled = false,
                    ProductionOnly = false,
                    Providers =
                    [
                        new AnalyticsProviderConfig
                        {
                            Type = "google-tag-manager",
                            ContainerId = "GTM-INACTIVE"
                        }
                    ]
                }
            }
        };

        Assert.Equal(
            RenderDependencyHasher.Compute(disabled, s_emptySiteModel, BuildExecutionMode.Production),
            RenderDependencyHasher.Compute(inactiveSettingsChanged, s_emptySiteModel, BuildExecutionMode.Development));
    }

    [Fact]
    public void Compute_AnalyticsPluginDisabled_RendererContractVersionChange_ProducesDifferentHash()
    {
        var disabled = CreateBaseConfig() with
        {
            Site = CreateBaseConfig().Site with
            {
                Plugins = new Dictionary<string, PluginToggleConfig>(StringComparer.OrdinalIgnoreCase)
                {
                    ["analytics"] = new() { Enabled = false }
                }
            }
        };

        Assert.NotEqual(
            RenderDependencyHasher.Compute(disabled, s_emptySiteModel, analyticsRendererContractVersion: "5"),
            RenderDependencyHasher.Compute(disabled, s_emptySiteModel, analyticsRendererContractVersion: "6"));
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
                            SnippetMode = "legacy",
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
                            SnippetMode = "legacy",
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
    public void Compute_PlausibleSnippetModeChange_ProducesDifferentHash()
    {
        var legacy = CreateBaseConfig() with
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
                            Domain = "example.com",
                            SnippetMode = "legacy",
                            ScriptUrl = "https://stats.example.com/tracker.js"
                        }
                    ]
                }
            }
        };
        var siteSpecific = legacy with
        {
            Site = legacy.Site with
            {
                Analytics = legacy.Site.Analytics with
                {
                    Providers =
                    [
                        new AnalyticsProviderConfig
                        {
                            Type = "plausible",
                            Domain = "example.com",
                            SnippetMode = "site-specific",
                            ScriptUrl = "https://stats.example.com/tracker.js"
                        }
                    ]
                }
            }
        };

        Assert.NotEqual(
            RenderDependencyHasher.Compute(legacy, s_emptySiteModel),
            RenderDependencyHasher.Compute(siteSpecific, s_emptySiteModel));
    }

    [Fact]
    public void Compute_GoogleConsentPolicyChange_ProducesDifferentHash()
    {
        static AnalyticsConsentConfig Consent(string adStorage, int? waitForUpdateMs) => new()
        {
            Google = new AnalyticsGoogleConsentConfig
            {
                Mode = "advanced",
                Defaults = new AnalyticsGoogleConsentDefaultsConfig
                {
                    AdStorage = adStorage,
                    AnalyticsStorage = "denied",
                    AdUserData = "denied",
                    AdPersonalization = "denied"
                },
                WaitForUpdateMs = waitForUpdateMs
            }
        };

        var baseline = CreateBaseConfig() with
        {
            Site = CreateBaseConfig().Site with
            {
                Analytics = new AnalyticsConfig
                {
                    Consent = Consent("denied", 500),
                    Providers = [CreateAnalyticsProvider()]
                }
            }
        };
        var stateChanged = baseline with
        {
            Site = baseline.Site with
            {
                Analytics = baseline.Site.Analytics with { Consent = Consent("granted", 500) }
            }
        };
        var waitChanged = baseline with
        {
            Site = baseline.Site with
            {
                Analytics = baseline.Site.Analytics with { Consent = Consent("denied", 250) }
            }
        };

        var baselineHash = RenderDependencyHasher.Compute(baseline, s_emptySiteModel);
        Assert.NotEqual(baselineHash, RenderDependencyHasher.Compute(stateChanged, s_emptySiteModel));
        Assert.NotEqual(baselineHash, RenderDependencyHasher.Compute(waitChanged, s_emptySiteModel));
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

    private static SiteModel SiteModelWithData(params (string Key, object Value)[] entries)
    {
        var data = new Dictionary<string, object>();
        foreach (var (key, value) in entries)
        {
            data[key] = value;
        }

        return s_emptySiteModel with { Data = data };
    }

    [Fact]
    public void Compute_DifferentSiteDataValue_ProducesDifferentHash()
    {
        var config = CreateBaseConfig();

        Assert.NotEqual(
            RenderDependencyHasher.Compute(config, SiteModelWithData(("banner", "alpha"))),
            RenderDependencyHasher.Compute(config, SiteModelWithData(("banner", "beta"))));
    }

    [Fact]
    public void Compute_DifferentModuleField_ProducesDifferentHash()
    {
        var config = CreateBaseConfig();

        SiteModel WithModuleContent(string content) => s_emptySiteModel with
        {
            Modules = new Dictionary<string, IReadOnlyList<ModuleInfo>>
            {
                ["team"] =
                [
                    new ModuleInfo { Id = "m1", Title = "Team", Slug = "team", Content = content }
                ]
            }
        };

        Assert.NotEqual(
            RenderDependencyHasher.Compute(config, WithModuleContent("first body")),
            RenderDependencyHasher.Compute(config, WithModuleContent("second body")));
    }

    [Fact]
    public void Compute_DifferentSequenceElements_ProducesDifferentHash()
    {
        var config = CreateBaseConfig();

        Assert.NotEqual(
            RenderDependencyHasher.Compute(config, SiteModelWithData(("items", new List<object> { "a", "b" }))),
            RenderDependencyHasher.Compute(config, SiteModelWithData(("items", new List<object> { "a", "c" }))));
    }

    [Fact]
    public void Compute_StringAndNumberWithSameText_ProducesDifferentHash()
    {
        var config = CreateBaseConfig();

        Assert.NotEqual(
            RenderDependencyHasher.Compute(config, SiteModelWithData(("value", "1"))),
            RenderDependencyHasher.Compute(config, SiteModelWithData(("value", 1))));
    }

    [Fact]
    public void Compute_NumericValue_IsCultureInvariant()
    {
        var config = CreateBaseConfig();
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            var invariantHash = RenderDependencyHasher.Compute(config, SiteModelWithData(("value", 1234.56)));

            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            var germanHash = RenderDependencyHasher.Compute(config, SiteModelWithData(("value", 1234.56)));

            Assert.Equal(invariantHash, germanHash);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void Compute_CyclicValue_FailsWithStableDiagnostic()
    {
        var config = CreateBaseConfig();
        var cyclic = new Dictionary<string, object?>();
        cyclic["self"] = cyclic;

        var exception = Assert.Throws<InvalidOperationException>(
            () => RenderDependencyHasher.Compute(config, SiteModelWithData(("cycle", cyclic))));

        Assert.Contains("render dependency value cycle", exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}
