using System.Security.Cryptography;
using Bukit.Config;
using Bukit.Engine.Incremental;
using Bukit.Rendering;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class RenderDependencyContributorInvalidationTests
{
    private static readonly SiteModel EmptySite = new()
    {
        Name = "test",
        Title = "test",
        BaseUrl = "/",
        Language = "en"
    };

    private static AppConfig Config() => new()
    {
        Site = new SiteConfig { Name = "test", Title = "Test Site" },
        Content = new ContentConfig(),
        Build = new BuildConfig { Output = "dist" },
        Theme = new ThemeConfig()
    };

    private static string Hash(IRenderDependencyContributor contributor, AppConfig config, SiteModel? site = null)
    {
        using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        contributor.Contribute(
            new RenderDependencyContext(config, site ?? EmptySite, BuildExecutionMode.Production, "v1"),
            new RenderDependencyHashWriter(hasher));
        return Convert.ToHexString(hasher.GetHashAndReset());
    }

    [Fact]
    public void SiteIdentityAndModes_ChangesIdentityButIgnoresLanguageOrder()
    {
        var contributor = new SiteIdentityAndModesContributor();
        var baseline = Hash(contributor, Config());
        Assert.Equal(baseline, Hash(contributor, Config()));
        Assert.NotEqual(baseline, Hash(contributor, Config() with { Site = Config().Site with { Title = "Other" } }));

        var first = Config() with { Site = Config().Site with { Languages = ["zh-CN", "en"] } };
        var second = Config() with { Site = Config().Site with { Languages = ["en", "zh-CN"] } };
        Assert.Equal(Hash(contributor, first), Hash(contributor, second));
    }

    [Fact]
    public void Analytics_ChangesEnabledConfigButIgnoresItWhenPluginIsDisabled()
    {
        var contributor = new AnalyticsContributor();
        var baseline = Hash(contributor, Config());
        Assert.Equal(baseline, Hash(contributor, Config()));
        Assert.NotEqual(baseline, Hash(contributor, Config() with
        {
            Site = Config().Site with { Analytics = new AnalyticsConfig { Enabled = false } }
        }));

        var disabled = Config() with
        {
            Site = Config().Site with
            {
                Plugins = new Dictionary<string, PluginToggleConfig> { ["analytics"] = new() { Enabled = false } }
            }
        };
        var disabledWithProviders = disabled with
        {
            Site = disabled.Site with
            {
                Analytics = new AnalyticsConfig
                {
                    Providers = [new AnalyticsProviderConfig { Type = "google-analytics", MeasurementId = "G-1" }]
                }
            }
        };
        Assert.Equal(Hash(contributor, disabled), Hash(contributor, disabledWithProviders));
    }

    [Fact]
    public void Seo_ChangesRenderedFieldButIgnoresDiagnostics()
    {
        var contributor = new SeoContributor();
        var baseline = Hash(contributor, Config());
        Assert.Equal(baseline, Hash(contributor, Config()));
        Assert.NotEqual(baseline, Hash(contributor, Config() with
        {
            Site = Config().Site with { Seo = new SeoConfig { RenderMode = "write" } }
        }));
        Assert.Equal(
            Hash(contributor, Config() with { Site = Config().Site with { Seo = new SeoConfig { Diagnostics = "warn" } } }),
            Hash(contributor, Config() with { Site = Config().Site with { Seo = new SeoConfig { Diagnostics = "error" } } }));
    }

    [Fact]
    public void ThemeAndTemplateModel_ChangesParamsButIgnoresShortcodeOrder()
    {
        var contributor = new ThemeAndTemplateModelContributor();
        var baseline = Hash(contributor, Config());
        Assert.Equal(baseline, Hash(contributor, Config()));
        Assert.NotEqual(baseline, Hash(contributor, Config() with
        {
            Theme = new ThemeConfig { Params = new Dictionary<string, object> { ["color"] = "red" } }
        }));

        var first = new ThemeConfig { Shortcodes = new Dictionary<string, string> { ["b"] = "B", ["a"] = "A" } };
        var second = new ThemeConfig { Shortcodes = new Dictionary<string, string> { ["a"] = "A", ["b"] = "B" } };
        Assert.Equal(
            Hash(contributor, Config() with { Theme = first }),
            Hash(contributor, Config() with { Theme = second }));
    }

    [Fact]
    public void CollectionsAndFieldScopes_ChangesPermalinkButIgnoresEmptyFilteredLists()
    {
        var contributor = new CollectionsAndFieldScopesContributor();
        var baseline = Config() with
        {
            Site = Config().Site with
            {
                Collections = new Dictionary<string, CollectionConfig>
                {
                    ["blog"] = new CollectionConfig { Permalink = "/blog/{slug}/" }
                }
            }
        };
        Assert.Equal(Hash(contributor, baseline), Hash(contributor, baseline));
        var changed = baseline with
        {
            Site = baseline.Site with
            {
                Collections = new Dictionary<string, CollectionConfig>
                {
                    ["blog"] = new CollectionConfig { Permalink = "/posts/{slug}/" }
                }
            }
        };
        Assert.NotEqual(Hash(contributor, baseline), Hash(contributor, changed));

        var empty = baseline with
        {
            Site = baseline.Site with
            {
                Collections = new Dictionary<string, CollectionConfig>
                {
                    ["blog"] = new CollectionConfig { Permalink = "/blog/{slug}/", FilteredLists = [] }
                }
            }
        };
        Assert.Equal(Hash(contributor, baseline), Hash(contributor, empty));
    }

    [Fact]
    public void Taxonomy_ChangesPageSizeButIgnoresEmptyKinds()
    {
        var contributor = new TaxonomyContributor();
        var baseline = Config();
        Assert.Equal(Hash(contributor, baseline), Hash(contributor, baseline));
        var changed = baseline with { Taxonomy = new TaxonomyConfig { PageSize = 20 } };
        Assert.NotEqual(Hash(contributor, baseline), Hash(contributor, changed));
        Assert.Equal(
            Hash(contributor, baseline with { Taxonomy = new TaxonomyConfig { Kinds = null } }),
            Hash(contributor, baseline with { Taxonomy = new TaxonomyConfig { Kinds = [] } }));
    }

    [Fact]
    public void RouteMetadata_ChangesSourceButIgnoresRequiredRouteOrder()
    {
        var contributor = new RouteMetadataConfigurationContributor();
        var first = Config() with
        {
            Content = Config().Content with
            {
                RouteMetadata = new RouteMetadataConfig { Source = "routes", RequiredRoutes = ["/a/", "/b/"] }
            }
        };
        var reordered = first with
        {
            Content = first.Content with
            {
                RouteMetadata = new RouteMetadataConfig { Source = "routes", RequiredRoutes = ["/b/", "/a/"] }
            }
        };
        Assert.Equal(Hash(contributor, first), Hash(contributor, reordered));
        Assert.NotEqual(Hash(contributor, first), Hash(contributor, first with
        {
            Content = first.Content with { RouteMetadata = new RouteMetadataConfig { Source = "other" } }
        }));
        Assert.Equal(Hash(contributor, Config()), Hash(contributor, Config()));
    }

    [Fact]
    public void NonAnalyticsPluginEnablement_ChangesOtherPluginsButIgnoresAnalytics()
    {
        var contributor = new NonAnalyticsPluginEnablementContributor();
        var enabled = Config() with
        {
            Site = Config().Site with
            {
                Plugins = new Dictionary<string, PluginToggleConfig> { ["seo"] = new() { Enabled = true } }
            }
        };
        var disabled = enabled with
        {
            Site = enabled.Site with
            {
                Plugins = new Dictionary<string, PluginToggleConfig> { ["seo"] = new() { Enabled = false } }
            }
        };
        Assert.Equal(Hash(contributor, enabled), Hash(contributor, enabled));
        Assert.NotEqual(Hash(contributor, enabled), Hash(contributor, disabled));

        var analyticsOff = enabled with
        {
            Site = enabled.Site with
            {
                Plugins = new Dictionary<string, PluginToggleConfig>
                {
                    ["seo"] = new() { Enabled = true },
                    ["analytics"] = new() { Enabled = false }
                }
            }
        };
        Assert.Equal(Hash(contributor, enabled), Hash(contributor, analyticsOff));
    }

    [Fact]
    public void SiteModelData_ChangesPublicDataButIgnoresInternalKeys()
    {
        var contributor = new SiteModelDataContributor();
        var baseline = EmptySite with { Data = new Dictionary<string, object> { ["pages"] = 1 } };
        Assert.Equal(Hash(contributor, Config(), baseline), Hash(contributor, Config(), baseline));
        Assert.NotEqual(
            Hash(contributor, Config(), baseline),
            Hash(contributor, Config(), EmptySite with { Data = new Dictionary<string, object> { ["pages"] = 2 } }));
        Assert.Equal(
            Hash(contributor, Config(), EmptySite with { Data = new Dictionary<string, object> { ["__internal"] = 1 } }),
            Hash(contributor, Config(), EmptySite with { Data = new Dictionary<string, object> { ["__internal"] = 2 } }));
    }
}
