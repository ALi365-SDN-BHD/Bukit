using Bukit.Config;
using Bukit.Shared;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class ConfigValidatorTests
{
    private static AppConfig ValidConfig(Func<AppConfig, AppConfig>? mutate = null)
    {
        var config = new AppConfig
        {
            Site = new SiteConfig
            {
                Name = "x",
                Title = "x"
            },
            Content = TestContent.Markdown()
        };
        return mutate != null ? mutate(config) : config;
    }

    private static AppConfig ConfigWithSite(Func<SiteConfig, SiteConfig> site) =>
        ValidConfig(c => c with { Site = site(c.Site) });

    private static AppConfig ConfigWithContent(Func<ContentConfig, ContentConfig> content) =>
        ValidConfig(c => c with { Content = content(c.Content) });

    private static AppConfig ConfigWithBuild(Func<BuildConfig, BuildConfig> build) =>
        ValidConfig(c => c with { Build = build(c.Build) });

    private static AppConfig ConfigWithTheme(Func<ThemeConfig, ThemeConfig> theme) =>
        ValidConfig(c => c with { Theme = theme(c.Theme) });

    [Fact]
    public void Validate_ValidConfig_Passes()
    {
        var config = ValidConfig();
        var ex = Record.Exception(() => ConfigValidator.Validate(config));
        Assert.Null(ex);
    }

    [Fact]
    public void Validate_MinimalDefaultConfig_Passes()
    {
        var config = new AppConfig
        {
            Site = new SiteConfig { Name = "x", Title = "x" },
            Content = TestContent.Markdown()
        };
        var ex = Record.Exception(() => ConfigValidator.Validate(config));
        Assert.Null(ex);
    }

    [Fact]
    public void Validate_SiteNameEmpty_Throws()
    {
        var config = ConfigWithSite(s => s with { Name = "" });
        var ex = Assert.Throws<ConfigException>(() => ConfigValidator.Validate(config));
        Assert.Equal("site.name is required.", ex.Message);
    }

    [Fact]
    public void Validate_SiteNameNull_Throws()
    {
        var config = ConfigWithSite(s => s with { Name = null! });
        var ex = Assert.Throws<ConfigException>(() => ConfigValidator.Validate(config));
        Assert.Equal("site.name is required.", ex.Message);
    }

    [Fact]
    public void Validate_SiteNameWhitespace_Throws()
    {
        var config = ConfigWithSite(s => s with { Name = "   " });
        var ex = Assert.Throws<ConfigException>(() => ConfigValidator.Validate(config));
        Assert.Equal("site.name is required.", ex.Message);
    }

    [Fact]
    public void Validate_SiteTitleEmpty_Throws()
    {
        var config = ConfigWithSite(s => s with { Title = "" });
        var ex = Assert.Throws<ConfigException>(() => ConfigValidator.Validate(config));
        Assert.Equal("site.title is required.", ex.Message);
    }

    [Fact]
    public void Validate_SiteUrlInvalid_Throws()
    {
        var config = ConfigWithSite(s => s with { Url = "ftp://example.com" });
        var ex = Assert.Throws<ConfigException>(() => ConfigValidator.Validate(config));
        Assert.Equal("site.url must start with http:// or https:// when set.", ex.Message);
    }

    [Fact]
    public void Validate_SiteUrlHttp_Passes()
    {
        var config = ConfigWithSite(s => s with { Url = "http://example.com" });
        var ex = Record.Exception(() => ConfigValidator.Validate(config));
        Assert.Null(ex);
    }

    [Fact]
    public void Validate_SiteUrlHttps_Passes()
    {
        var config = ConfigWithSite(s => s with { Url = "https://example.com" });
        var ex = Record.Exception(() => ConfigValidator.Validate(config));
        Assert.Null(ex);
    }

    [Fact]
    public void Validate_AutoSummaryMaxLengthZero_Throws()
    {
        var config = ConfigWithSite(s => s with { AutoSummaryMaxLength = 0 });
        var ex = Assert.Throws<ConfigException>(() => ConfigValidator.Validate(config));
        Assert.Equal("site.autoSummaryMaxLength must be between 1 and 5000.", ex.Message);
    }

    [Fact]
    public void Validate_AutoSummaryMaxLengthOver5000_Throws()
    {
        var config = ConfigWithSite(s => s with { AutoSummaryMaxLength = 5001 });
        var ex = Assert.Throws<ConfigException>(() => ConfigValidator.Validate(config));
        Assert.Equal("site.autoSummaryMaxLength must be between 1 and 5000.", ex.Message);
    }

    [Fact]
    public void Validate_AutoSummaryMaxLengthValid_Passes()
    {
        var config = ConfigWithSite(s => s with { AutoSummaryMaxLength = 100 });
        var ex = Record.Exception(() => ConfigValidator.Validate(config));
        Assert.Null(ex);
    }

    [Fact]
    public void Validate_BaseUrlEmpty_Throws()
    {
        var config = ConfigWithSite(s => s with { BaseUrl = "" });
        var ex = Assert.Throws<ConfigException>(() => ConfigValidator.Validate(config));
        Assert.Equal("site.baseUrl is required.", ex.Message);
    }

    [Fact]
    public void Validate_BaseUrlDoesNotStartWithSlash_Throws()
    {
        var config = ConfigWithSite(s => s with { BaseUrl = "blog/" });
        var ex = Assert.Throws<ConfigException>(() => ConfigValidator.Validate(config));
        Assert.Equal("site.baseUrl must start with '/'.", ex.Message);
    }

    [Fact]
    public void Validate_BaseUrlSlash_Passes()
    {
        var config = ConfigWithSite(s => s with { BaseUrl = "/" });
        var ex = Record.Exception(() => ConfigValidator.Validate(config));
        Assert.Null(ex);
    }

    [Fact]
    public void Validate_OutputPathEncodingInvalid_Throws()
    {
        var config = ConfigWithSite(s => s with { OutputPathEncoding = "invalid" });
        var ex = Assert.Throws<ConfigException>(() => ConfigValidator.Validate(config));
        Assert.Equal("site.outputPathEncoding must be none|slug|urlencode|sanitize.", ex.Message);
    }

    [Theory]
    [InlineData("none")]
    [InlineData("slug")]
    [InlineData("urlencode")]
    [InlineData("sanitize")]
    public void Validate_OutputPathEncodingValid_Passes(string encoding)
    {
        var config = ConfigWithSite(s => s with { OutputPathEncoding = encoding });
        var ex = Record.Exception(() => ConfigValidator.Validate(config));
        Assert.Null(ex);
    }

    [Fact]
    public void Validate_ThemeComponentValidationInvalid_Throws()
    {
        var config = ConfigWithTheme(t => t with { ComponentValidation = "silent" });
        var ex = Assert.Throws<ConfigException>(() => ConfigValidator.Validate(config));
        Assert.Equal("theme.componentValidation must be off|warn|strict.", ex.Message);
    }

    [Theory]
    [InlineData("off")]
    [InlineData("warn")]
    [InlineData("strict")]
    public void Validate_ThemeComponentValidationValid_Passes(string mode)
    {
        var config = ConfigWithTheme(t => t with { ComponentValidation = mode });
        var ex = Record.Exception(() => ConfigValidator.Validate(config));
        Assert.Null(ex);
    }

    [Fact]
    public void Validate_LanguagesEmpty_Throws()
    {
        var config = ConfigWithSite(s => s with { Languages = new[] { "", "  " } });
        var ex = Assert.Throws<ConfigException>(() => ConfigValidator.Validate(config));
        Assert.Equal("site.languages must contain at least one language.", ex.Message);
    }

    [Fact]
    public void Validate_LanguagesDuplicates_Throws()
    {
        var config = ConfigWithSite(s => s with { Languages = new[] { "zh", "en", "zh" } });
        var ex = Assert.Throws<ConfigException>(() => ConfigValidator.Validate(config));
        Assert.Equal("site.languages has duplicate language: zh", ex.Message);
    }

    [Fact]
    public void Validate_DefaultLanguageNotInLanguages_Throws()
    {
        var config = ConfigWithSite(s => s with
        {
            Languages = new[] { "zh", "en" },
            DefaultLanguage = "fr"
        });
        var ex = Assert.Throws<ConfigException>(() => ConfigValidator.Validate(config));
        Assert.Equal("site.defaultLanguage must be included in site.languages.", ex.Message);
    }

    [Fact]
    public void Validate_LanguagesValid_Passes()
    {
        var config = ConfigWithSite(s => s with
        {
            Languages = new[] { "zh", "en" },
            DefaultLanguage = "zh"
        });
        var ex = Record.Exception(() => ConfigValidator.Validate(config));
        Assert.Null(ex);
    }

    [Fact]
    public void Validate_SitemapModeInvalid_Throws()
    {
        var config = ConfigWithSite(s => s with { SitemapMode = "invalid" });
        var ex = Assert.Throws<ConfigException>(() => ConfigValidator.Validate(config));
        Assert.Equal("site.sitemapMode must be split|merged|index.", ex.Message);
    }

    [Theory]
    [InlineData("split")]
    [InlineData("merged")]
    [InlineData("index")]
    public void Validate_SitemapModeValid_Passes(string mode)
    {
        var config = ConfigWithSite(s => s with { SitemapMode = mode });
        var ex = Record.Exception(() => ConfigValidator.Validate(config));
        Assert.Null(ex);
    }

    [Fact]
    public void Validate_RssModeInvalid_Throws()
    {
        var config = ConfigWithSite(s => s with { RssMode = "index" });
        var ex = Assert.Throws<ConfigException>(() => ConfigValidator.Validate(config));
        Assert.Equal("site.rssMode must be split|merged.", ex.Message);
    }

    [Theory]
    [InlineData("split")]
    [InlineData("merged")]
    public void Validate_RssModeValid_Passes(string mode)
    {
        var config = ConfigWithSite(s => s with { RssMode = mode });
        var ex = Record.Exception(() => ConfigValidator.Validate(config));
        Assert.Null(ex);
    }

    [Fact]
    public void Validate_Timezone_IanaAsiaShanghai_Passes()
    {
        var config = ConfigWithSite(s => s with { Timezone = "Asia/Shanghai" });
        var ex = Record.Exception(() => ConfigValidator.Validate(config));
        Assert.Null(ex);
    }

    [Fact]
    public void TryGetWindowsTimeZoneFallback_AsiaShanghai_ReturnsChinaStandardTime()
    {
        var ok = TimeZoneCompatibility.TryGetWindowsTimeZoneFallback("Asia/Shanghai", out var windowsTimeZoneId);
        Assert.True(ok);
        Assert.Equal("China Standard Time", windowsTimeZoneId);
    }

    [Fact]
    public void Validate_SearchModeInvalid_Throws()
    {
        var config = ConfigWithSite(s => s with { SearchMode = "invalid" });
        var ex = Assert.Throws<ConfigException>(() => ConfigValidator.Validate(config));
        Assert.Equal("site.searchMode must be split|merged|index.", ex.Message);
    }

    [Theory]
    [InlineData("split")]
    [InlineData("merged")]
    [InlineData("index")]
    public void Validate_SearchModeValid_Passes(string mode)
    {
        var config = ConfigWithSite(s => s with { SearchMode = mode });
        var ex = Record.Exception(() => ConfigValidator.Validate(config));
        Assert.Null(ex);
    }

    [Fact]
    public void Validate_PluginFailModeInvalid_Throws()
    {
        var config = ConfigWithSite(s => s with { PluginFailMode = "ignore" });
        var ex = Assert.Throws<ConfigException>(() => ConfigValidator.Validate(config));
        Assert.Equal("site.pluginFailMode must be strict|warn.", ex.Message);
    }

    [Theory]
    [InlineData("strict")]
    [InlineData("warn")]
    public void Validate_PluginFailModeValid_Passes(string mode)
    {
        var config = ConfigWithSite(s => s with { PluginFailMode = mode });
        var ex = Record.Exception(() => ConfigValidator.Validate(config));
        Assert.Null(ex);
    }

    [Fact]
    public void Validate_ContentProviderEmpty_Throws()
    {
        var config = ConfigWithContent(c => c with { Sources = Array.Empty<ContentSourceConfig>() });
        var ex = Assert.Throws<ConfigException>(() => ConfigValidator.Validate(config));
        Assert.Equal("content.sources is required in Bukit 1.0.", ex.Message);
    }

    [Fact]
    public void Validate_BuildOutputEmpty_Throws()
    {
        var config = ConfigWithBuild(b => b with { Output = "" });
        var ex = Assert.Throws<ConfigException>(() => ConfigValidator.Validate(config));
        Assert.Equal("build.output is required.", ex.Message);
    }

    [Fact]
    public void ConfigLoader_Permalinks_LoadedFromYaml()
    {
        var yaml = """
            site:
              name: test
              title: Test
              baseUrl: /
              permalinks:
                post: "/{year}/{month}/{slug}/"
                page: "/docs/{slug}/"
            content:
              sources:
                - type: markdown
                  name: page
                  collection: page
                  markdown:
                    dir: content
            build:
              output: dist
            """;

        var tmpFile = Path.Combine(Path.GetTempPath(), $"ssp-test-{Guid.NewGuid():N}.yaml");
        try
        {
            File.WriteAllText(tmpFile, yaml);
            var config = ConfigLoader.Load(tmpFile);

            Assert.NotNull(config.Site.Permalinks);
            Assert.Equal(2, config.Site.Permalinks!.Count);
            Assert.Equal("/{year}/{month}/{slug}/", config.Site.Permalinks["post"]);
            Assert.Equal("/docs/{slug}/", config.Site.Permalinks["page"]);
        }
        finally
        {
            File.Delete(tmpFile);
        }
    }

    [Fact]
    public void ConfigLoader_Permalinks_NullWhenNotConfigured()
    {
        var yaml = """
            site:
              name: test
              title: Test
              baseUrl: /
            content:
              sources:
                - type: markdown
                  name: page
                  collection: page
                  markdown:
                    dir: content
            build:
              output: dist
            """;

        var tmpFile = Path.Combine(Path.GetTempPath(), $"ssp-test-{Guid.NewGuid():N}.yaml");
        try
        {
            File.WriteAllText(tmpFile, yaml);
            var config = ConfigLoader.Load(tmpFile);

            Assert.Null(config.Site.Permalinks);
        }
        finally
        {
            File.Delete(tmpFile);
        }
    }

    [Fact]
    public void ConfigLoader_SeoAndAnalytics_LoadedFromYaml()
    {
        var yaml = """
            site:
              name: test
              title: Test
              url: https://example.com/
              baseUrl: /docs/
              seo:
                enabled: true
                renderMode: inject
                diagnostics: strict
                defaultImage: /assets/og.png
                twitterSite: "@bukit"
                robotsTxt:
                  enabled: true
                schema:
                  webPage: false
                  collectionPage: false
                  searchAction: false
                organization:
                  name: Example Inc
                  url: https://example.com/about
                  logo: https://example.com/logo.png
              analytics:
                enabled: false
                google_analytics_id: G-ABC123
                disableInPreview: false
            content:
              sources:
                - type: markdown
                  name: page
                  collection: page
                  markdown:
                    dir: content
            build:
              output: dist
            """;

        var tmpFile = Path.Combine(Path.GetTempPath(), $"ssp-test-{Guid.NewGuid():N}.yaml");
        try
        {
            File.WriteAllText(tmpFile, yaml);
            var config = ConfigLoader.Load(tmpFile);

            Assert.True(config.Site.Seo.Enabled);
            Assert.Equal("inject", config.Site.Seo.RenderMode);
            Assert.Equal("strict", config.Site.Seo.Diagnostics);
            Assert.Equal("/assets/og.png", config.Site.Seo.DefaultImage);
            Assert.Equal("@bukit", config.Site.Seo.TwitterSite);
            Assert.True(config.Site.Seo.RobotsTxt.Enabled);
            Assert.False(config.Site.Seo.Schema.WebPage);
            Assert.False(config.Site.Seo.Schema.CollectionPage);
            Assert.False(config.Site.Seo.Schema.SearchAction);
            Assert.Equal("Example Inc", config.Site.Seo.Organization?.Name);
            Assert.Equal("https://example.com/about", config.Site.Seo.Organization?.Url);
            Assert.Equal("https://example.com/logo.png", config.Site.Seo.Organization?.Logo);
            Assert.False(config.Site.Analytics.Enabled);
            Assert.Equal("G-ABC123", config.Site.Analytics.GoogleAnalyticsId);
            Assert.False(config.Site.Analytics.DisableInPreview);
        }
        finally
        {
            File.Delete(tmpFile);
        }
    }

    [Fact]
    public void Validate_MinimalDefaultConfig_HasSeoAndAnalyticsDefaults()
    {
        var config = ValidConfig();

        Assert.True(config.Site.Seo.Enabled);
        Assert.Equal("inject", config.Site.Seo.RenderMode);
        Assert.Equal("warn", config.Site.Seo.Diagnostics);
        Assert.False(config.Site.Seo.RobotsTxt.Enabled);
        Assert.True(config.Site.Seo.Schema.WebPage);
        Assert.True(config.Site.Seo.Schema.CollectionPage);
        Assert.True(config.Site.Seo.Schema.SearchAction);
        Assert.True(config.Site.Analytics.Enabled);
        Assert.True(config.Site.Analytics.DisableInPreview);
        Assert.Null(config.Site.Analytics.GoogleAnalyticsId);
    }

    [Fact]
    public void Validate_SeoRenderModeInvalid_Throws()
    {
        var config = ConfigWithSite(s => s with { Seo = s.Seo with { RenderMode = "auto" } });
        var ex = Assert.Throws<ConfigException>(() => ConfigValidator.Validate(config));
        Assert.Equal("site.seo.renderMode must be theme|inject|off.", ex.Message);
    }

    [Fact]
    public void Validate_AnalyticsGoogleAnalyticsIdInvalid_Throws()
    {
        var config = ConfigWithSite(s => s with { Analytics = s.Analytics with { GoogleAnalyticsId = "UA-123" } });
        var ex = Assert.Throws<ConfigException>(() => ConfigValidator.Validate(config));
        Assert.Equal("site.analytics.google_analytics_id must be a GA4 id starting with G-.", ex.Message);
    }

    [Fact]
    public void Validate_Collections_InvalidPermalink_Throws()
    {
        var config = ValidConfig() with
        {
            Site = ValidConfig().Site with
            {
                Collections = new Dictionary<string, CollectionConfig>(StringComparer.OrdinalIgnoreCase)
                {
                    ["article"] = new()
                    {
                        Permalink = "/articles/",
                        Template = "pages/post.html"
                    }
                }
            }
        };

        var ex = Assert.Throws<ConfigException>(() => ConfigValidator.Validate(config));
        Assert.Contains("must include {slug}", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_Collections_InvalidListRoute_Throws()
    {
        var config = ValidConfig() with
        {
            Site = ValidConfig().Site with
            {
                Collections = new Dictionary<string, CollectionConfig>(StringComparer.OrdinalIgnoreCase)
                {
                    ["article"] = new()
                    {
                        Permalink = "/articles/{slug}/",
                        Template = "pages/post.html",
                        ListRoute = "articles"
                    }
                }
            }
        };

        var ex = Assert.Throws<ConfigException>(() => ConfigValidator.Validate(config));
        Assert.Contains("listRoute", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_Collections_ValidConfig_Passes()
    {
        var config = ValidConfig() with
        {
            Site = ValidConfig().Site with
            {
                Collections = new Dictionary<string, CollectionConfig>(StringComparer.OrdinalIgnoreCase)
                {
                    ["article"] = new()
                    {
                        Permalink = "/articles/{slug}/",
                        Template = "pages/post.html",
                        ListRoute = "/articles/",
                        Pagination = new CollectionPaginationConfig
                        {
                            Enabled = true,
                            PageSize = 12
                        },
                        Output = new CollectionOutputConfig
                        {
                            Rss = true,
                            Sitemap = true,
                            Archive = true
                        }
                    }
                }
            }
        };

        ConfigValidator.Validate(config);
    }

    [Fact]
    public void Validate_ExternalPlugins_ProcessRuntime_ValidConfig_Passes()
    {
        var config = ValidConfig() with
        {
            Site = ValidConfig().Site with
            {
                ExternalPlugins = new Dictionary<string, ExternalPluginConfig>(StringComparer.OrdinalIgnoreCase)
                {
                    ["sample"] = new()
                    {
                        Runtime = "process",
                        Entry = "plugins/sample-plugin.exe",
                        Hooks = new[] { "after-build" },
                        Capabilities = new[] { "emit-outputs" },
                        Enabled = true,
                        TimeoutMs = 5000,
                        Options = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["mode"] = "demo"
                        }
                    }
                }
            }
        };

        var ex = Record.Exception(() => ConfigValidator.Validate(config));
        Assert.Null(ex);
    }

    [Fact]
    public void Validate_ExternalPlugins_RuntimeMissing_Throws()
    {
        var config = ValidConfig() with
        {
            Site = ValidConfig().Site with
            {
                ExternalPlugins = new Dictionary<string, ExternalPluginConfig>(StringComparer.OrdinalIgnoreCase)
                {
                    ["sample"] = new()
                    {
                        Runtime = "",
                        Entry = "plugins/sample-plugin.exe",
                        Hooks = new[] { "after-build" },
                        TimeoutMs = 5000
                    }
                }
            }
        };

        var ex = Assert.Throws<ConfigException>(() => ConfigValidator.Validate(config));
        Assert.Contains("runtime", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_ExternalPlugins_EntryMissing_Throws()
    {
        var config = ValidConfig() with
        {
            Site = ValidConfig().Site with
            {
                ExternalPlugins = new Dictionary<string, ExternalPluginConfig>(StringComparer.OrdinalIgnoreCase)
                {
                    ["sample"] = new()
                    {
                        Runtime = "process",
                        Entry = "",
                        Hooks = new[] { "after-build" },
                        TimeoutMs = 5000
                    }
                }
            }
        };

        var ex = Assert.Throws<ConfigException>(() => ConfigValidator.Validate(config));
        Assert.Contains("entry", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_ExternalPlugins_HooksEmpty_Throws()
    {
        var config = ValidConfig() with
        {
            Site = ValidConfig().Site with
            {
                ExternalPlugins = new Dictionary<string, ExternalPluginConfig>(StringComparer.OrdinalIgnoreCase)
                {
                    ["sample"] = new()
                    {
                        Runtime = "process",
                        Entry = "plugins/sample-plugin.exe",
                        Hooks = Array.Empty<string>(),
                        TimeoutMs = 5000
                    }
                }
            }
        };

        var ex = Assert.Throws<ConfigException>(() => ConfigValidator.Validate(config));
        Assert.Contains("hooks", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_ExternalPlugins_TimeoutInvalid_Throws()
    {
        var config = ValidConfig() with
        {
            Site = ValidConfig().Site with
            {
                ExternalPlugins = new Dictionary<string, ExternalPluginConfig>(StringComparer.OrdinalIgnoreCase)
                {
                    ["sample"] = new()
                    {
                        Runtime = "process",
                        Entry = "plugins/sample-plugin.exe",
                        Hooks = new[] { "after-build" },
                        TimeoutMs = 0
                    }
                }
            }
        };

        var ex = Assert.Throws<ConfigException>(() => ConfigValidator.Validate(config));
        Assert.Contains("timeout", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // DESKTOP-REMOVED: wasm runtime tests disabled (AOT-only).
#if false
    [Fact]
    public void Validate_ExternalPlugins_RuntimeWasm_IsRecognized()
    {
        var config = ValidConfig() with
        {
            Site = ValidConfig().Site with
            {
                ExternalPlugins = new Dictionary<string, ExternalPluginConfig>(StringComparer.OrdinalIgnoreCase)
                {
                    ["sample"] = new()
                    {
                        Runtime = "wasm",
                        Entry = "plugins/sample-plugin.wasm",
                        Hooks = new[] { "after-build" },
                        TimeoutMs = 5000
                    }
                }
            }
        };

        var ex = Record.Exception(() => ConfigValidator.Validate(config));
        Assert.Null(ex);
    }

    [Fact]
    public void Validate_ExternalPlugins_HookDerivePages_IsRecognized()
    {
        var config = ValidConfig() with
        {
            Site = ValidConfig().Site with
            {
                ExternalPlugins = new Dictionary<string, ExternalPluginConfig>(StringComparer.OrdinalIgnoreCase)
                {
                    ["sample"] = new()
                    {
                        Runtime = "process",
                        Entry = "plugins/sample-plugin.exe",
                        Hooks = new[] { "after-build", "derive-pages" },
                        TimeoutMs = 5000
                    }
                }
            }
        };

        var ex = Record.Exception(() => ConfigValidator.Validate(config));
        Assert.Null(ex);
    }

    [Fact]
    public void Validate_ExternalPlugins_HookUnknown_Throws()
    {
        var config = ValidConfig() with
        {
            Site = ValidConfig().Site with
            {
                ExternalPlugins = new Dictionary<string, ExternalPluginConfig>(StringComparer.OrdinalIgnoreCase)
                {
                    ["sample"] = new()
                    {
                        Runtime = "process",
                        Entry = "plugins/sample-plugin.exe",
                        Hooks = new[] { "after-build", "before-render" },
                        TimeoutMs = 5000
                    }
                }
            }
        };

        var ex = Assert.Throws<ConfigException>(() => ConfigValidator.Validate(config));
        Assert.Contains("hooks", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_ExternalPlugins_WasmProfileInvalid_Throws()
    {
        var config = ValidConfig() with
        {
            Site = ValidConfig().Site with
            {
                ExternalPlugins = new Dictionary<string, ExternalPluginConfig>(StringComparer.OrdinalIgnoreCase)
                {
                    ["sample"] = new()
                    {
                        Runtime = "wasm",
                        Entry = "plugins/sample-plugin.wasm",
                        Hooks = new[] { "after-build" },
                        Capabilities = new[] { "emit-outputs" },
                        TimeoutMs = 5000,
                        WasmProfile = "unknown-profile"
                    }
                }
            }
        };

        var ex = Assert.Throws<ConfigException>(() => ConfigValidator.Validate(config));
        Assert.Contains("wasmProfile", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_ExternalPlugins_WasmMaxMemoryInvalid_Throws()
    {
        var config = ValidConfig() with
        {
            Site = ValidConfig().Site with
            {
                ExternalPlugins = new Dictionary<string, ExternalPluginConfig>(StringComparer.OrdinalIgnoreCase)
                {
                    ["sample"] = new()
                    {
                        Runtime = "wasm",
                        Entry = "plugins/sample-plugin.wasm",
                        Hooks = new[] { "after-build" },
                        Capabilities = new[] { "emit-outputs" },
                        TimeoutMs = 5000,
                        MaxMemoryMb = 0
                    }
                }
            }
        };

        var ex = Assert.Throws<ConfigException>(() => ConfigValidator.Validate(config));
        Assert.Contains("maxMemoryMb", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_ExternalPlugins_WasmMaxMemoryTooLarge_Throws()
    {
        var config = ValidConfig() with
        {
            Site = ValidConfig().Site with
            {
                ExternalPlugins = new Dictionary<string, ExternalPluginConfig>(StringComparer.OrdinalIgnoreCase)
                {
                    ["sample"] = new()
                    {
                        Runtime = "wasm",
                        Entry = "plugins/sample-plugin.wasm",
                        Hooks = new[] { "after-build" },
                        Capabilities = new[] { "emit-outputs" },
                        TimeoutMs = 5000,
                        MaxMemoryMb = 1024
                    }
                }
            }
        };

        var ex = Assert.Throws<ConfigException>(() => ConfigValidator.Validate(config));
        Assert.Contains("maxMemoryMb", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_ExternalPlugins_WasmFsModeInvalid_Throws()
    {
        var config = ValidConfig() with
        {
            Site = ValidConfig().Site with
            {
                ExternalPlugins = new Dictionary<string, ExternalPluginConfig>(StringComparer.OrdinalIgnoreCase)
                {
                    ["sample"] = new()
                    {
                        Runtime = "wasm",
                        Entry = "plugins/sample-plugin.wasm",
                        Hooks = new[] { "after-build" },
                        TimeoutMs = 5000,
                        WasmFsMode = "full"
                    }
                }
            }
        };

        var ex = Assert.Throws<ConfigException>(() => ConfigValidator.Validate(config));
        Assert.Contains("wasmFsMode", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_ExternalPlugins_WasmAllowNetworkTrue_Throws()
    {
        var config = ValidConfig() with
        {
            Site = ValidConfig().Site with
            {
                ExternalPlugins = new Dictionary<string, ExternalPluginConfig>(StringComparer.OrdinalIgnoreCase)
                {
                    ["sample"] = new()
                    {
                        Runtime = "wasm",
                        Entry = "plugins/sample-plugin.wasm",
                        Hooks = new[] { "after-build" },
                        TimeoutMs = 5000,
                        WasmAllowNetwork = true
                    }
                }
            }
        };

        var ex = Assert.Throws<ConfigException>(() => ConfigValidator.Validate(config));
        Assert.Contains("wasmAllowNetwork", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_ExternalPlugins_WasmCapabilityUnsupported_Throws()
    {
        var config = ValidConfig() with
        {
            Site = ValidConfig().Site with
            {
                ExternalPlugins = new Dictionary<string, ExternalPluginConfig>(StringComparer.OrdinalIgnoreCase)
                {
                    ["sample"] = new()
                    {
                        Runtime = "wasm",
                        Entry = "plugins/sample-plugin.wasm",
                        Hooks = new[] { "after-build" },
                        TimeoutMs = 5000,
                        Capabilities = new[] { "emit-outputs", "network" }
                    }
                }
            }
        };

        var ex = Assert.Throws<ConfigException>(() => ConfigValidator.Validate(config));
        Assert.Contains("capabilities", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
#endif

    [Fact]
    public void Validate_ExternalPlugins_ProcessOptionsArguments_Throws()
    {
        var config = ValidConfig() with
        {
            Site = ValidConfig().Site with
            {
                ExternalPlugins = new Dictionary<string, ExternalPluginConfig>(StringComparer.OrdinalIgnoreCase)
                {
                    ["sample"] = new()
                    {
                        Runtime = "process",
                        Entry = "plugins/sample-plugin.exe",
                        Hooks = new[] { "after-build" },
                        Capabilities = new[] { "emit-outputs" },
                        TimeoutMs = 5000,
                        Options = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["arguments"] = "--mode demo"
                        }
                    }
                }
            }
        };

        var ex = Assert.Throws<ConfigException>(() => ConfigValidator.Validate(config));
        Assert.Contains("options.arguments", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_ExternalPlugins_ProcessOptionsNamedArgsInvalidKey_Throws()
    {
        var config = ValidConfig() with
        {
            Site = ValidConfig().Site with
            {
                ExternalPlugins = new Dictionary<string, ExternalPluginConfig>(StringComparer.OrdinalIgnoreCase)
                {
                    ["sample"] = new()
                    {
                        Runtime = "process",
                        Entry = "plugins/sample-plugin.exe",
                        Hooks = new[] { "after-build" },
                        Capabilities = new[] { "emit-outputs" },
                        TimeoutMs = 5000,
                        Options = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["processArgs"] = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                            {
                                ["named"] = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                                {
                                    ["mode;rm"] = "demo"
                                }
                            }
                        }
                    }
                }
            }
        };

        var ex = Assert.Throws<ConfigException>(() => ConfigValidator.Validate(config));
        Assert.Contains("options.processArgs.named", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_ExternalPlugins_ProcessOptionsNamedArgsValid_Passes()
    {
        var config = ValidConfig() with
        {
            Site = ValidConfig().Site with
            {
                ExternalPlugins = new Dictionary<string, ExternalPluginConfig>(StringComparer.OrdinalIgnoreCase)
                {
                    ["sample"] = new()
                    {
                        Runtime = "process",
                        Entry = "plugins/sample-plugin.exe",
                        Hooks = new[] { "after-build" },
                        Capabilities = new[] { "emit-outputs" },
                        TimeoutMs = 5000,
                        Options = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["processArgs"] = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                            {
                                ["positionals"] = new List<object> { "plugin.dll", "success" },
                                ["named"] = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                                {
                                    ["retry"] = "3",
                                    ["dry-run"] = true
                                }
                            }
                        }
                    }
                }
            }
        };

        var ex = Record.Exception(() => ConfigValidator.Validate(config));
        Assert.Null(ex);
    }

    // DESKTOP-REMOVED: ExternalAssembly validation tests disabled (AOT-only).
#if false
    [Fact]
    public void Validate_Site_ExternalAssemblyTrustModeStrictWithoutAllowlist_Throws()
    {
        var config = ValidConfig() with
        {
            Site = ValidConfig().Site with
            {
                ExternalAssemblyTrustMode = "strict",
                ExternalAssemblyAllowlist = null
            }
        };

        var ex = Assert.Throws<ConfigException>(() => ConfigValidator.Validate(config));
        Assert.Contains("externalAssemblyAllowlist", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_Site_ExternalAssemblyAllowlistHashInvalid_Throws()
    {
        var config = ValidConfig() with
        {
            Site = ValidConfig().Site with
            {
                ExternalAssemblyTrustMode = "warn",
                ExternalAssemblyAllowlist = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["ThrowingPlugin.dll"] = "abc123"
                }
            }
        };

        var ex = Assert.Throws<ConfigException>(() => ConfigValidator.Validate(config));
        Assert.Contains("externalAssemblyAllowlist", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_Site_ExternalAssemblyAllowlistValid_Passes()
    {
        var config = ValidConfig() with
        {
            Site = ValidConfig().Site with
            {
                ExternalAssemblyTrustMode = "strict",
                ExternalAssemblyAllowlist = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["ThrowingPlugin.dll"] = new string('a', 64)
                }
            }
        };

        var ex = Record.Exception(() => ConfigValidator.Validate(config));
        Assert.Null(ex);
    }
#endif

    [Fact]
    public void Validate_Site_DeriveConflictPolicy_LastWins_Passes()
    {
        var config = ValidConfig() with
        {
            Site = ValidConfig().Site with
            {
                DeriveConflictPolicy = "last-wins"
            }
        };

        var ex = Record.Exception(() => ConfigValidator.Validate(config));
        Assert.Null(ex);
    }

    [Fact]
    public void Validate_Site_DeriveConflictPolicy_Invalid_Throws()
    {
        var config = ValidConfig() with
        {
            Site = ValidConfig().Site with
            {
                DeriveConflictPolicy = "overwrite"
            }
        };

        var ex = Assert.Throws<ConfigException>(() => ConfigValidator.Validate(config));
        Assert.Contains("deriveConflictPolicy", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_ExternalPlugins_AbsoluteEntryPath_RejectedByDefault()
    {
        var config = ValidConfig() with
        {
            Site = ValidConfig().Site with
            {
                ExternalPlugins = new Dictionary<string, ExternalPluginConfig>(StringComparer.OrdinalIgnoreCase)
                {
                    ["sample"] = new()
                    {
                        Runtime = "process",
                        Entry = "/usr/bin/some-tool",
                        Hooks = new[] { "after-build" },
                        TimeoutMs = 5000
                    }
                }
            }
        };

        var ex = Assert.Throws<ConfigException>(() => ConfigValidator.Validate(config));
        Assert.Contains("entry must be within project directory", ex.Message);
    }

    [Fact]
    public void Validate_ExternalPlugins_AbsoluteEntryPath_AllowedWhenAllowAbsoluteEntryTrue()
    {
        var config = ValidConfig() with
        {
            Site = ValidConfig().Site with
            {
                ExternalPlugins = new Dictionary<string, ExternalPluginConfig>(StringComparer.OrdinalIgnoreCase)
                {
                    ["sample"] = new()
                    {
                        Runtime = "process",
                        Entry = "/usr/bin/some-tool",
                        Hooks = new[] { "after-build" },
                        Capabilities = new[] { "emit-outputs" },
                        TimeoutMs = 5000,
                        AllowAbsoluteEntry = true
                    }
                }
            }
        };

        var ex = Record.Exception(() => ConfigValidator.Validate(config));
        Assert.Null(ex);
    }

    [Fact]
    public void Validate_ExternalPlugins_RelativeEntryPath_Accepted()
    {
        var config = ValidConfig() with
        {
            Site = ValidConfig().Site with
            {
                ExternalPlugins = new Dictionary<string, ExternalPluginConfig>(StringComparer.OrdinalIgnoreCase)
                {
                    ["sample"] = new()
                    {
                        Runtime = "process",
                        Entry = "plugins/my-plugin.js",
                        Hooks = new[] { "after-build" },
                        Capabilities = new[] { "emit-outputs" },
                        TimeoutMs = 5000
                    }
                }
            }
        };

        var ex = Record.Exception(() => ConfigValidator.Validate(config));
        Assert.Null(ex);
    }

    [Fact]
    public void Validate_ExternalPlugins_WindowsAbsoluteEntryPath_RejectedByDefault()
    {
        var config = ValidConfig() with
        {
            Site = ValidConfig().Site with
            {
                ExternalPlugins = new Dictionary<string, ExternalPluginConfig>(StringComparer.OrdinalIgnoreCase)
                {
                    ["sample"] = new()
                    {
                        Runtime = "process",
                        Entry = "C:\\tools\\plugin.exe",
                        Hooks = new[] { "after-build" },
                        TimeoutMs = 5000
                    }
                }
            }
        };

        if (OperatingSystem.IsWindows())
        {
            var ex = Assert.Throws<ConfigException>(() => ConfigValidator.Validate(config));
            Assert.Contains("entry must be within project directory", ex.Message);
        }
    }
}
