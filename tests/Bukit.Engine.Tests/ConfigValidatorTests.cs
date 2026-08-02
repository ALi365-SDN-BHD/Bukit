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

    private static AppConfig ConfigWithFilteredList(FilteredListConfig filter) =>
        ValidConfig() with
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
                        FilteredLists = new[] { filter }
                    }
                }
            }
        };

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

    [Theory]
    [InlineData("../outside")]
    [InlineData("en/us")]
    [InlineData("en\\us")]
    [InlineData("/tmp")]
    [InlineData("C:\\temp")]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("en_US")]
    [InlineData("en\nadmin")]
    public void Validate_LanguageIsNotSafePortableSegment_Throws(string language)
    {
        var config = ConfigWithSite(s => s with
        {
            Languages = new[] { "en", language },
            DefaultLanguage = "en"
        });

        var ex = Assert.Throws<ConfigException>(() => ConfigValidator.Validate(config));

        Assert.Equal("site.languages must use safe alphanumeric subtags separated by hyphens.", ex.Message);
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
            Languages = new[] { "zh-Hans", "pt-BR", "en" },
            DefaultLanguage = "zh-Hans"
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
    public void Validate_FeedModeInvalid_Throws()
    {
        var config = ConfigWithSite(s => s with { Feed = s.Feed with { Mode = "index" } });
        var ex = Assert.Throws<ConfigException>(() => ConfigValidator.Validate(config));
        Assert.Equal("site.feed configuration produced an invalid feed mode; expected split|merged.", ex.Message);
    }

    [Theory]
    [InlineData("split")]
    [InlineData("merged")]
    public void Validate_FeedModeValid_Passes(string mode)
    {
        var config = ConfigWithSite(s => s with { Feed = s.Feed with { Mode = mode } });
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
    public void Validate_SearchConfigModeInvalid_Throws()
    {
        var config = ConfigWithSite(s => s with { Search = s.Search with { Mode = "invalid" } });
        var ex = Assert.Throws<ConfigException>(() => ConfigValidator.Validate(config));
        Assert.Equal("site.search.mode must be split|merged|index.", ex.Message);
    }

    [Theory]
    [InlineData("split")]
    [InlineData("merged")]
    [InlineData("index")]
    public void Validate_SearchConfigModeValid_Passes(string mode)
    {
        var config = ConfigWithSite(s => s with { Search = s.Search with { Mode = mode } });
        var ex = Record.Exception(() => ConfigValidator.Validate(config));
        Assert.Null(ex);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_SearchMaxContentLengthNonPositive_Throws(int maxContentLength)
    {
        var config = ConfigWithSite(s => s with
        {
            Search = s.Search with { MaxContentLength = maxContentLength }
        });

        var ex = Assert.Throws<ConfigException>(() => ConfigValidator.Validate(config));

        Assert.Equal(DiagnosticCode.ConfigInvalidValue, ex.Code);
        Assert.Equal("site.search.maxContentLength must be positive.", ex.Message);
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
                productionOnly: false
                providers:
                  - type: google-analytics
                    measurementId: G-ABC123
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
            Assert.False(config.Site.Analytics.ProductionOnly);
            var provider = Assert.Single(config.Site.Analytics.Providers);
            Assert.Equal("google-analytics", provider.Type);
            Assert.Equal("G-ABC123", provider.MeasurementId);
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
        Assert.Equal("{siteTitle}", config.Site.Seo.HomeTitleTemplate);
        Assert.Equal("{pageTitle}", config.Site.Seo.PageTitleTemplate);
        Assert.Equal(" | ", config.Site.Seo.TitleSeparator);
        Assert.False(config.Site.Seo.RobotsTxt.Enabled);
        Assert.True(config.Site.Seo.Schema.WebPage);
        Assert.True(config.Site.Seo.Schema.CollectionPage);
        Assert.True(config.Site.Seo.Schema.SearchAction);
        Assert.True(config.Site.Analytics.Enabled);
        Assert.True(config.Site.Analytics.ProductionOnly);
        Assert.Empty(config.Site.Analytics.Providers);
    }

    [Fact]
    public void Validate_SeoRenderModeInvalid_Throws()
    {
        var config = ConfigWithSite(s => s with { Seo = s.Seo with { RenderMode = "auto" } });
        var ex = Assert.Throws<ConfigException>(() => ConfigValidator.Validate(config));
        Assert.Equal("site.seo.renderMode must be theme|inject|off.", ex.Message);
    }

    [Theory]
    [InlineData("{pageTitle}{unknown}", "site.seo.pageTitleTemplate contains unsupported placeholder {unknown}.")]
    [InlineData("pageTitle}", "site.seo.pageTitleTemplate contains an unopened placeholder.")]
    [InlineData("{pageTitle", "site.seo.pageTitleTemplate contains an unclosed placeholder.")]
    [InlineData("{siteTitle}", "site.seo.pageTitleTemplate must contain {pageTitle}.")]
    public void Validate_SeoPageTitleTemplateInvalid_Throws(string template, string expectedMessage)
    {
        var config = ConfigWithSite(s => s with
        {
            Seo = s.Seo with { PageTitleTemplate = template }
        });

        var ex = Assert.Throws<ConfigException>(() => ConfigValidator.Validate(config));

        Assert.Equal(expectedMessage, ex.Message);
    }

    [Fact]
    public void Validate_SeoHomeTitleTemplateWithoutDynamicPlaceholder_Throws()
    {
        var config = ConfigWithSite(s => s with
        {
            Seo = s.Seo with { HomeTitleTemplate = "Fixed title" }
        });

        var ex = Assert.Throws<ConfigException>(() => ConfigValidator.Validate(config));

        Assert.Equal("site.seo.homeTitleTemplate must contain {pageTitle} or {siteTitle}.", ex.Message);
    }

    [Fact]
    public void Validate_SeoTitleSeparatorMayBeEmpty()
    {
        var config = ConfigWithSite(site => site with
        {
            Seo = site.Seo with
            {
                PageTitleTemplate = "{pageTitle}{separator}{siteTitle}",
                TitleSeparator = string.Empty
            }
        });

        ConfigValidator.Validate(config);
    }

    [Fact]
    public void Validate_AnalyticsMeasurementIdInvalid_Throws()
    {
        var config = ConfigWithSite(s => s with
        {
            Analytics = s.Analytics with
            {
                Providers =
                [
                    new AnalyticsProviderConfig
                    {
                        Type = "google-analytics",
                        MeasurementId = "UA-123"
                    }
                ]
            }
        });
        var ex = Assert.Throws<ConfigException>(() => ConfigValidator.Validate(config));
        Assert.Equal("site.analytics.providers[0].measurementId must match ^G-[A-Z0-9]+$.", ex.Message);
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

    [Theory]
    [InlineData("")]
    [InlineData("/page/{page}/")]
    [InlineData("https://example.com/page/{page}/")]
    [InlineData("../page/{page}/")]
    [InlineData("page/")]
    [InlineData("page/{page}?sort=asc")]
    [InlineData("page/{page}#top")]
    [InlineData("page\\{page}\\")]
    [InlineData("page/%2F/{page}/")]
    [InlineData("page/{section}/{page}/")]
    [InlineData("page/{page")]
    public void Validate_Collections_InvalidPaginationUrlPattern_Throws(string urlPattern)
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
                            PageSize = 10,
                            UrlPattern = urlPattern
                        }
                    }
                }
            }
        };

        var ex = Assert.Throws<ConfigException>(() => ConfigValidator.Validate(config));
        Assert.Contains("urlPattern", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("page/:num/")]
    [InlineData("page/{num}/")]
    [InlineData("p/{page}/")]
    [InlineData("p/{page}")]
    [InlineData("{collection}/{slug}/p/{page}/")]
    [InlineData("{Collection}/{Slug}/p/{Page}/")]
    public void Validate_Collections_ValidPaginationUrlPattern_Passes(string urlPattern)
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
                            PageSize = 10,
                            UrlPattern = urlPattern
                        }
                    }
                }
            }
        };

        ConfigValidator.Validate(config);
    }

    [Fact]
    public void Validate_FilteredLists_InvalidPageSize_Throws()
    {
        var config = ConfigWithFilteredList(new FilteredListConfig
        {
            Field = "country",
            Value = "Malaysia",
            ListRoute = "/articles/malaysia/",
            PageSize = 0
        });

        var ex = Assert.Throws<ConfigException>(() => ConfigValidator.Validate(config));

        Assert.Contains("filteredLists[0].pageSize", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_FilteredLists_InvalidOperator_Throws()
    {
        var config = ConfigWithFilteredList(new FilteredListConfig
        {
            Field = "country",
            Operator = "startsWith",
            Value = "Malaysia",
            ListRoute = "/articles/malaysia/"
        });

        var ex = Assert.Throws<ConfigException>(() => ConfigValidator.Validate(config));

        Assert.Contains("filteredLists[0].operator", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("equals")]
    [InlineData("contains")]
    public void Validate_FilteredLists_SingleValueOperatorWithoutValue_Throws(string filterOperator)
    {
        var config = ConfigWithFilteredList(new FilteredListConfig
        {
            Field = "country",
            Operator = filterOperator,
            ListRoute = "/articles/malaysia/"
        });

        var ex = Assert.Throws<ConfigException>(() => ConfigValidator.Validate(config));

        Assert.Contains("filteredLists[0].value", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_FilteredLists_InOperatorWithoutValues_Throws()
    {
        var config = ConfigWithFilteredList(new FilteredListConfig
        {
            Field = "category",
            Operator = "in",
            ListRoute = "/articles/market/"
        });

        var ex = Assert.Throws<ConfigException>(() => ConfigValidator.Validate(config));

        Assert.Contains("filteredLists[0].values", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_FilteredLists_SingleValueOperatorWithValues_Throws()
    {
        var config = ConfigWithFilteredList(new FilteredListConfig
        {
            Field = "category",
            Operator = "equals",
            Value = "市场观察",
            Values = new[] { "市场观察", "政策动态" },
            ListRoute = "/articles/market/"
        });

        var ex = Assert.Throws<ConfigException>(() => ConfigValidator.Validate(config));

        Assert.Contains("filteredLists[0].values", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_FilteredLists_InOperatorWithValue_Throws()
    {
        var config = ConfigWithFilteredList(new FilteredListConfig
        {
            Field = "category",
            Operator = "in",
            Value = "市场观察",
            Values = new[] { "市场观察", "政策动态" },
            ListRoute = "/articles/market/"
        });

        var ex = Assert.Throws<ConfigException>(() => ConfigValidator.Validate(config));

        Assert.Contains("filteredLists[0].value", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("")]
    [InlineData("/page/{page}/")]
    [InlineData("https://example.com/page/{page}/")]
    [InlineData("../page/{page}/")]
    [InlineData("page/")]
    [InlineData("page/{page}?sort=asc")]
    [InlineData("page/{page}#top")]
    [InlineData("page\\{page}\\")]
    [InlineData("page/%2F/{page}/")]
    [InlineData("page/{section}/{page}/")]
    [InlineData("page/{page")]
    public void Validate_FilteredLists_InvalidUrlPattern_Throws(string urlPattern)
    {
        var config = ConfigWithFilteredList(new FilteredListConfig
        {
            Field = "country",
            Value = "Malaysia",
            ListRoute = "/articles/malaysia/",
            UrlPattern = urlPattern
        });

        var ex = Assert.Throws<ConfigException>(() => ConfigValidator.Validate(config));

        Assert.Contains("filteredLists[0].urlPattern", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_FilteredLists_InvalidEmptyBehavior_Throws()
    {
        var config = ConfigWithFilteredList(new FilteredListConfig
        {
            Field = "country",
            Value = "Malaysia",
            ListRoute = "/articles/malaysia/",
            EmptyBehavior = "drop"
        });

        var ex = Assert.Throws<ConfigException>(() => ConfigValidator.Validate(config));

        Assert.Contains("filteredLists[0].emptyBehavior", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_FilteredLists_BlankListTemplate_Throws()
    {
        var config = ConfigWithFilteredList(new FilteredListConfig
        {
            Field = "country",
            Value = "Malaysia",
            ListRoute = "/articles/malaysia/",
            ListTemplate = "   "
        });

        var ex = Assert.Throws<ConfigException>(() => ConfigValidator.Validate(config));

        Assert.Contains("filteredLists[0].listTemplate", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_FilteredLists_PaginationConfig_Passes()
    {
        var config = ConfigWithFilteredList(new FilteredListConfig
        {
            Field = "country",
            Value = "Malaysia",
            ListRoute = "/articles/malaysia/",
            ListTemplate = "pages/filter.html",
            PageSize = 2,
            UrlPattern = "page/{page}/",
            EmptyBehavior = "skip"
        });

        ConfigValidator.Validate(config);
    }

    [Fact]
    public void Validate_FilteredLists_InOperatorWithValues_Passes()
    {
        var config = ConfigWithFilteredList(new FilteredListConfig
        {
            Field = "category",
            Operator = "in",
            Values = new[] { "市场观察", "政策动态" },
            ListRoute = "/articles/market/"
        });

        ConfigValidator.Validate(config);
    }

    [Theory]
    [InlineData("/insights/category")]
    [InlineData("/insights/category/")]
    [InlineData("/分类/category")]
    public void Validate_TaxonomyKindRoutePrefix_ValidValues_Passes(string routePrefix)
    {
        var config = ValidConfig() with
        {
            Taxonomy = new TaxonomyConfig
            {
                Kinds = new[]
                {
                    new TaxonomyKindConfig
                    {
                        Key = "categories",
                        Kind = "category",
                        RoutePrefix = routePrefix
                    }
                }
            }
        };

        ConfigValidator.Validate(config);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("insights/category")]
    [InlineData("//example.com/category")]
    [InlineData("https://example.com/category")]
    [InlineData("/insights/../category")]
    [InlineData("/insights/%2Fcategory")]
    [InlineData("/insights/category?x=1")]
    public void Validate_TaxonomyKindRoutePrefix_InvalidValues_Throws(string routePrefix)
    {
        var config = ValidConfig() with
        {
            Taxonomy = new TaxonomyConfig
            {
                Kinds = new[]
                {
                    new TaxonomyKindConfig
                    {
                        Key = "categories",
                        Kind = "category",
                        RoutePrefix = routePrefix
                    }
                }
            }
        };

        var ex = Assert.Throws<ConfigException>(() => ConfigValidator.Validate(config));
        Assert.Contains("routePrefix", ex.Message, StringComparison.OrdinalIgnoreCase);
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

}
