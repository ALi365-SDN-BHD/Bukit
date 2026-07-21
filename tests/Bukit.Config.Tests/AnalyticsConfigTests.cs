using Bukit.Shared;
using Xunit;

namespace Bukit.Config.Tests;

public sealed class AnalyticsConfigTests : IDisposable
{
    private readonly List<string> _tempFiles = new();

    public void Dispose()
    {
        foreach (var path in _tempFiles)
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Defaults_AreEnabledProductionOnlyWithNoProviders()
    {
        var analytics = new AnalyticsConfig();

        Assert.True(analytics.Enabled);
        Assert.True(analytics.ProductionOnly);
        Assert.Empty(analytics.Providers);
    }

    [Theory]
    [InlineData("google-analytics", "measurementId: G-CONSENT123")]
    [InlineData("google-tag-manager", "containerId: GTM-CONSENT123")]
    public void Validate_GoogleProviderWithoutExplicitConsent_ThrowsRequiredField(
        string providerType,
        string providerField)
    {
        var config = LoadAnalytics($$"""
            providers:
              - type: {{providerType}}
                {{providerField}}
            """);

        var ex = Assert.Throws<ConfigException>(() => ConfigValidator.Validate(config));

        Assert.Equal(DiagnosticCode.ConfigRequiredFieldMissing, ex.Code);
        Assert.Equal("site.analytics.consent.google is required when a Google provider is configured.", ex.Message);
    }

    [Fact]
    public void Load_ExplicitGoogleConsent_PreservesV2DefaultsAndWaitPolicy()
    {
        var config = LoadAnalytics("""
            consent:
              google:
                mode: advanced
                defaults:
                  adStorage: denied
                  analyticsStorage: granted
                  adUserData: denied
                  adPersonalization: granted
                waitForUpdateMs: 500
            providers:
              - type: google-analytics
                measurementId: G-CONSENT123
            """);

        ConfigValidator.Validate(config);

        var google = Assert.IsType<AnalyticsGoogleConsentConfig>(config.Site.Analytics.Consent?.Google);
        Assert.Equal("advanced", google.Mode);
        Assert.Equal(500, google.WaitForUpdateMs);
        var defaults = Assert.IsType<AnalyticsGoogleConsentDefaultsConfig>(google.Defaults);
        Assert.Equal("denied", defaults.AdStorage);
        Assert.Equal("granted", defaults.AnalyticsStorage);
        Assert.Equal("denied", defaults.AdUserData);
        Assert.Equal("granted", defaults.AdPersonalization);
    }

    [Fact]
    public void Validate_EmptyConsentMapping_ThrowsRequiredGoogleField()
    {
        var config = LoadAnalytics("consent: {}");

        var ex = Assert.Throws<ConfigException>(() => ConfigValidator.Validate(config));

        Assert.Equal(DiagnosticCode.ConfigRequiredFieldMissing, ex.Code);
        Assert.Equal("site.analytics.consent.google is required when site.analytics.consent is configured.", ex.Message);
    }

    [Theory]
    [InlineData("consent: []", "site.analytics.consent must be a mapping.")]
    [InlineData("consent:\n  google: []", "site.analytics.consent.google must be a mapping.")]
    [InlineData("consent:\n  google:\n    defaults: []", "site.analytics.consent.google.defaults must be a mapping.")]
    public void Load_GoogleConsentWrongKind_ThrowsInvalidValue(string consentYaml, string expectedMessage)
    {
        var ex = Assert.Throws<ConfigException>(() => LoadAnalytics(consentYaml));

        Assert.Equal(DiagnosticCode.ConfigInvalidValue, ex.Code);
        Assert.Equal(expectedMessage, ex.Message);
    }

    [Theory]
    [InlineData("mode", "basic", DiagnosticCode.ConfigInvalidValue)]
    [InlineData("adStorage", "unknown", DiagnosticCode.ConfigInvalidValue)]
    [InlineData("analyticsStorage", "", DiagnosticCode.ConfigRequiredFieldMissing)]
    [InlineData("adUserData", "GRANTED", DiagnosticCode.ConfigInvalidValue)]
    [InlineData("adPersonalization", "Denied", DiagnosticCode.ConfigInvalidValue)]
    public void Validate_GoogleConsentInvalidModeOrState_Throws(
        string field,
        string value,
        DiagnosticCode expectedCode)
    {
        var mode = field == "mode" ? value : "advanced";
        var adStorage = field == "adStorage" ? value : "denied";
        var analyticsStorage = field == "analyticsStorage" ? value : "denied";
        var adUserData = field == "adUserData" ? value : "denied";
        var adPersonalization = field == "adPersonalization" ? value : "denied";
        var config = LoadAnalytics($$"""
            consent:
              google:
                mode: {{mode}}
                defaults:
                  adStorage: {{adStorage}}
                  analyticsStorage: {{analyticsStorage}}
                  adUserData: {{adUserData}}
                  adPersonalization: {{adPersonalization}}
            providers:
              - type: google-analytics
                measurementId: G-CONSENT123
            """);

        var ex = Assert.Throws<ConfigException>(() => ConfigValidator.Validate(config));

        Assert.Equal(expectedCode, ex.Code);
        Assert.Contains(field, ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(5001)]
    public void Validate_GoogleConsentWaitOutsideSupportedRange_Throws(int waitForUpdateMs)
    {
        var config = LoadAnalytics($$"""
            consent:
              google:
                mode: advanced
                defaults:
                  adStorage: denied
                  analyticsStorage: denied
                  adUserData: denied
                  adPersonalization: denied
                waitForUpdateMs: {{waitForUpdateMs}}
            providers:
              - type: google-analytics
                measurementId: G-CONSENT123
            """);

        var ex = Assert.Throws<ConfigException>(() => ConfigValidator.Validate(config));

        Assert.Equal(DiagnosticCode.ConfigInvalidValue, ex.Code);
        Assert.Contains("waitForUpdateMs", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_GoogleConsentWithoutGoogleProvider_ThrowsInvalidValue()
    {
        var config = LoadAnalytics("""
            consent:
              google:
                mode: advanced
                defaults:
                  adStorage: denied
                  analyticsStorage: denied
                  adUserData: denied
                  adPersonalization: denied
            providers:
              - type: umami
                websiteId: 89f9c547-2017-4b05-8a56-8f40b488f927
                scriptUrl: https://analytics.example.com/script.js
            """);

        var ex = Assert.Throws<ConfigException>(() => ConfigValidator.Validate(config));

        Assert.Equal(DiagnosticCode.ConfigInvalidValue, ex.Code);
        Assert.Contains("requires a Google provider", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Load_CspRequirementsReport_PreservesExplicitMode()
    {
        var config = LoadAnalytics("""
            csp:
              mode: requirements-report
            providers:
              - type: umami
                websiteId: 89f9c547-2017-4b05-8a56-8f40b488f927
                scriptUrl: https://analytics.example.com/script.js
            """);

        ConfigValidator.Validate(config);

        Assert.Equal("requirements-report", config.Site.Analytics.Csp?.Mode);
    }

    [Fact]
    public void Load_CspWrongKind_ThrowsInvalidValue()
    {
        var ex = Assert.Throws<ConfigException>(() => LoadAnalytics("csp: []"));

        Assert.Equal(DiagnosticCode.ConfigInvalidValue, ex.Code);
        Assert.Equal("site.analytics.csp must be a mapping.", ex.Message);
    }

    [Fact]
    public void Validate_CspInvalidMode_ThrowsInvalidValue()
    {
        var config = LoadAnalytics("""
            csp:
              mode: nonce
            providers:
              - type: umami
                websiteId: 89f9c547-2017-4b05-8a56-8f40b488f927
                scriptUrl: https://analytics.example.com/script.js
            """);

        var ex = Assert.Throws<ConfigException>(() => ConfigValidator.Validate(config));

        Assert.Equal(DiagnosticCode.ConfigInvalidValue, ex.Code);
        Assert.Contains("requirements-report", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_CspRequirementsReportWithBuildReportDisabled_ThrowsInvalidValue()
    {
        var loaded = LoadAnalytics("""
            csp:
              mode: requirements-report
            providers:
              - type: umami
                websiteId: 89f9c547-2017-4b05-8a56-8f40b488f927
                scriptUrl: https://analytics.example.com/script.js
            """);
        var config = loaded with
        {
            Build = loaded.Build with
            {
                Report = loaded.Build.Report with { Enabled = false }
            }
        };

        var ex = Assert.Throws<ConfigException>(() => ConfigValidator.Validate(config));

        Assert.Equal(DiagnosticCode.ConfigInvalidValue, ex.Code);
        Assert.Contains("build.report.enabled", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Load_AllSupportedProviders_PreservesOrderAndExplicitPlausibleLegacyUrl()
    {
        var config = LoadAnalytics("""
            enabled: false
            productionOnly: false
            consent:
              google:
                mode: advanced
                defaults:
                  adStorage: denied
                  analyticsStorage: denied
                  adUserData: denied
                  adPersonalization: denied
            providers:
              - type: google-analytics
                measurementId: G-ABC123
              - type: google-tag-manager
                containerId: GTM-XYZ789
              - type: plausible
                domain: bücher.example
                snippetMode: legacy
                scriptUrl: https://plausible.io/js/script.js
              - type: umami
                websiteId: 89f9c547-2017-4b05-8a56-8f40b488f927
                scriptUrl: https://analytics.example.com/script.js
            """);

        ConfigValidator.Validate(config);

        Assert.False(config.Site.Analytics.Enabled);
        Assert.False(config.Site.Analytics.ProductionOnly);
        Assert.Equal(
            ["google-analytics", "google-tag-manager", "plausible", "umami"],
            config.Site.Analytics.Providers.Select(provider => provider.Type));
        Assert.Equal("G-ABC123", config.Site.Analytics.Providers[0].MeasurementId);
        Assert.Equal("GTM-XYZ789", config.Site.Analytics.Providers[1].ContainerId);
        Assert.Equal("bücher.example", config.Site.Analytics.Providers[2].Domain);
        Assert.Equal("https://plausible.io/js/script.js", config.Site.Analytics.Providers[2].ScriptUrl);
        Assert.Equal("89f9c547-2017-4b05-8a56-8f40b488f927", config.Site.Analytics.Providers[3].WebsiteId);
    }

    [Fact]
    public void Load_PlausibleWithoutScriptUrl_DoesNotInjectLegacyDefault()
    {
        var config = LoadAnalytics("""
            providers:
              - type: plausible
                domain: example.com
            """);

        Assert.Null(Assert.Single(config.Site.Analytics.Providers).ScriptUrl);
    }

    [Fact]
    public void Validate_PlausibleWithoutScriptUrl_ThrowsRequiredField()
    {
        var ex = Assert.Throws<ConfigException>(() =>
        {
            var config = LoadAnalytics("""
                providers:
                  - type: plausible
                    domain: example.com
                    snippetMode: legacy
                """);
            ConfigValidator.Validate(config);
        });

        Assert.Equal(DiagnosticCode.ConfigRequiredFieldMissing, ex.Code);
        Assert.Contains("scriptUrl is required", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_PlausibleSiteSpecificScript_Passes()
    {
        var config = LoadAnalytics("""
            providers:
              - type: plausible
                domain: example.com
                snippetMode: site-specific
                scriptUrl: https://plausible.io/js/pa-AN07TEST.js
            """);

        ConfigValidator.Validate(config);
    }

    [Fact]
    public void Validate_PlausibleWithoutSnippetMode_ThrowsRequiredField()
    {
        var config = LoadAnalytics("""
            providers:
              - type: plausible
                domain: example.com
                scriptUrl: https://plausible.io/js/script.js
            """);

        var ex = Assert.Throws<ConfigException>(() => ConfigValidator.Validate(config));

        Assert.Equal(DiagnosticCode.ConfigRequiredFieldMissing, ex.Code);
        Assert.Contains("snippetMode is required", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("site-specific", "https://plausible.io/js/script.js")]
    [InlineData("legacy", "https://plausible.io/js/pa-AN07TEST.js")]
    public void Validate_PlausibleOfficialUrlMustMatchSnippetMode_Throws(
        string snippetMode,
        string scriptUrl)
    {
        var config = LoadAnalytics($$"""
            providers:
              - type: plausible
                domain: example.com
                snippetMode: {{snippetMode}}
                scriptUrl: {{scriptUrl}}
            """);

        var ex = Assert.Throws<ConfigException>(() => ConfigValidator.Validate(config));

        Assert.Contains("snippetMode", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("automatic")]
    [InlineData("site_specific")]
    public void Validate_InvalidPlausibleSnippetMode_Throws(string snippetMode)
    {
        var config = WithProvider(new AnalyticsProviderConfig
        {
            Type = "plausible",
            Domain = "example.com",
            SnippetMode = snippetMode,
            ScriptUrl = "https://stats.example.com/tracker.js"
        });

        var ex = Assert.Throws<ConfigException>(() => ConfigValidator.Validate(config));

        Assert.Contains("snippetMode", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("googleAnalyticsId")]
    [InlineData("disableInPreview")]
    public void Load_LegacyField_ThrowsUnknownField(string field)
    {
        var ex = Assert.Throws<ConfigException>(() => LoadAnalytics($"{field}: legacy"));

        Assert.Equal(DiagnosticCode.ConfigInvalidValue, ex.Code);
        Assert.Equal($"Unknown config field 'site.analytics.{field}'.", ex.Message);
    }

    public static TheoryData<string, string> InvalidAnalyticsContractNodes => new()
    {
        { "analytics: []", "site.analytics must be a mapping." },
        { "analytics:\n  providers: {}", "site.analytics.providers must be a sequence." },
        { "analytics:\n  enabled: []", "site.analytics.enabled must be a boolean." },
        { "analytics:\n  productionOnly: {}", "site.analytics.productionOnly must be a boolean." },
        { "plugins:\n  analytics: definitely-not-a-bool", "site.plugins.analytics must be a mapping or boolean." },
        { "plugins: []", "site.plugins must be a mapping." },
        { "plugins:\n  analytics:\n    enabled: []", "site.plugins.analytics.enabled must be a boolean." },
        { "plugins:\n  analytics:\n    options: []", "site.plugins.analytics.options must be a mapping." },
        { "plugins:\n  analytics:\n    typo: true", "Unknown config field 'site.plugins.analytics.typo'." }
    };

    [Theory]
    [MemberData(nameof(InvalidAnalyticsContractNodes))]
    public void Load_InvalidAnalyticsContractNode_ThrowsInvalidValue(string siteFragment, string expectedMessage)
    {
        var ex = Assert.Throws<ConfigException>(() => LoadSiteFragment(siteFragment));

        Assert.Equal(DiagnosticCode.ConfigInvalidValue, ex.Code);
        Assert.Equal(expectedMessage, ex.Message);
    }

    [Theory]
    [InlineData("analytics: {}")]
    [InlineData("analytics:\n  providers: []")]
    public void Load_ValidEmptyAnalyticsNodes_KeepDefaults(string siteFragment)
    {
        var config = LoadSiteFragment(siteFragment);

        Assert.True(config.Site.Analytics.Enabled);
        Assert.True(config.Site.Analytics.ProductionOnly);
        Assert.Empty(config.Site.Analytics.Providers);
    }

    [Theory]
    [InlineData("google-analytics", "containerId:")]
    [InlineData("google-analytics", "snippetMode: legacy")]
    [InlineData("google-tag-manager", "measurementId:")]
    [InlineData("plausible", "websiteId:")]
    [InlineData("umami", "domain:")]
    public void Load_ProviderWithFieldOwnedByAnotherType_EvenWhenEmpty_ThrowsUnknownField(string type, string field)
    {
        var ex = Assert.Throws<ConfigException>(() => LoadAnalytics($$"""
                providers:
                  - type: {{type}}
                    {{field}}
                """));

        Assert.Contains("Unknown config field", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_PlausibleWithExplicitEmptyScriptUrl_DoesNotUseDefaultAndThrows()
    {
        var config = LoadAnalytics("""
            providers:
              - type: plausible
                domain: example.com
                snippetMode: legacy
                scriptUrl:
            """);

        var ex = Assert.Throws<ConfigException>(() => ConfigValidator.Validate(config));

        Assert.Contains("scriptUrl", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Load_ProviderWithUnknownField_ThrowsUnknownField()
    {
        var ex = Assert.Throws<ConfigException>(() => LoadAnalytics("""
            providers:
              - type: plausible
                domain: example.com
                customScript: alert(1)
            """));

        Assert.Equal(DiagnosticCode.ConfigInvalidValue, ex.Code);
        Assert.Equal("Unknown config field 'site.analytics.providers[0].customScript'.", ex.Message);
    }

    [Theory]
    [InlineData("google-analytics")]
    [InlineData("google-tag-manager")]
    [InlineData("plausible")]
    [InlineData("umami")]
    public void Validate_ProviderMissingRequiredField_Throws(string type)
    {
        var config = LoadAnalytics($$"""
            providers:
              - type: {{type}}
            """);

        var ex = Assert.Throws<ConfigException>(() => ConfigValidator.Validate(config));

        Assert.Contains("is required", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Load_ProviderMissingType_Throws()
    {
        var ex = Assert.Throws<ConfigException>(() => LoadAnalytics("""
            providers:
              - measurementId: G-ABC123
            """));

        Assert.Contains("type", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Google-Analytics")]
    [InlineData("google_analytics")]
    [InlineData("unknown")]
    public void Validate_UnsupportedProviderType_Throws(string type)
    {
        var config = WithProvider(new AnalyticsProviderConfig { Type = type });

        var ex = Assert.Throws<ConfigException>(() => ConfigValidator.Validate(config));

        Assert.Contains("type must be", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("UA-123")]
    [InlineData("G-abc123")]
    [InlineData(" G-ABC123")]
    public void Validate_InvalidGoogleAnalyticsMeasurementId_Throws(string measurementId)
    {
        var config = WithProvider(new AnalyticsProviderConfig
        {
            Type = "google-analytics",
            MeasurementId = measurementId
        });

        Assert.Throws<ConfigException>(() => ConfigValidator.Validate(config));
    }

    [Theory]
    [InlineData("")]
    [InlineData("GTM-")]
    [InlineData("gtm-ABC123")]
    [InlineData(" GTM-ABC123")]
    public void Validate_InvalidGoogleTagManagerContainerId_Throws(string containerId)
    {
        var config = WithProvider(new AnalyticsProviderConfig
        {
            Type = "google-tag-manager",
            ContainerId = containerId
        });

        Assert.Throws<ConfigException>(() => ConfigValidator.Validate(config));
    }

    [Theory]
    [InlineData("https://example.com")]
    [InlineData("example.com:443")]
    [InlineData("example.com/path")]
    [InlineData("user@example.com")]
    [InlineData("127.0.0.1")]
    [InlineData("-bad.example")]
    [InlineData("bad..example")]
    public void Validate_InvalidPlausibleDomain_Throws(string domain)
    {
        var config = WithProvider(new AnalyticsProviderConfig
        {
            Type = "plausible",
            Domain = domain,
            SnippetMode = "legacy",
            ScriptUrl = "https://plausible.io/js/script.js"
        });

        Assert.Throws<ConfigException>(() => ConfigValidator.Validate(config));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-uuid")]
    [InlineData("{89f9c547-2017-4b05-8a56-8f40b488f927}")]
    public void Validate_InvalidUmamiWebsiteId_Throws(string websiteId)
    {
        var config = WithProvider(new AnalyticsProviderConfig
        {
            Type = "umami",
            WebsiteId = websiteId,
            ScriptUrl = "https://analytics.example.com/script.js"
        });

        Assert.Throws<ConfigException>(() => ConfigValidator.Validate(config));
    }

    [Theory]
    [InlineData("http://example.com/script.js")]
    [InlineData("/script.js")]
    [InlineData("https://user:pass@example.com/script.js")]
    [InlineData("https://example.com/script.js#fragment")]
    [InlineData("https://example.com/script")]
    [InlineData("https://example.com/script.css")]
    public void Validate_InvalidScriptUrl_Throws(string scriptUrl)
    {
        var config = WithProvider(new AnalyticsProviderConfig
        {
            Type = "umami",
            WebsiteId = "89f9c547-2017-4b05-8a56-8f40b488f927",
            ScriptUrl = scriptUrl
        });

        Assert.Throws<ConfigException>(() => ConfigValidator.Validate(config));
    }

    [Fact]
    public void Validate_DuplicateProviderKey_Throws()
    {
        var provider = new AnalyticsProviderConfig
        {
            Type = "google-analytics",
            MeasurementId = "G-ABC123"
        };
        var config = WithProviders(provider, provider with { });

        var ex = Assert.Throws<ConfigException>(() => ConfigValidator.Validate(config));

        Assert.Contains("duplicate", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_IdnAndAsciiEquivalentDomains_AreDuplicateProviderKeys()
    {
        var config = WithProviders(
            new AnalyticsProviderConfig
            {
                Type = "plausible", Domain = "bücher.example",
                SnippetMode = "legacy",
                ScriptUrl = "https://plausible.io/js/script.js"
            },
            new AnalyticsProviderConfig
            {
                Type = "plausible", Domain = "xn--bcher-kva.example",
                SnippetMode = "legacy",
                ScriptUrl = "https://plausible.io/js/script.js"
            });

        var ex = Assert.Throws<ConfigException>(() => ConfigValidator.Validate(config));

        Assert.Contains("duplicate", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private AppConfig LoadAnalytics(string analytics)
    {
        var indentedAnalytics = string.Join(
            Environment.NewLine,
            analytics.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n').Select(line => $"    {line}"));
        var yaml = $$"""
            site:
              name: analytics-test
              title: Analytics Test
              analytics:
            {{indentedAnalytics}}
            content:
              sources:
                - type: markdown
                  markdown:
                    dir: content
            """;
        var path = Path.Combine(Path.GetTempPath(), $"bukit-analytics-config-{Guid.NewGuid():N}.yaml");
        File.WriteAllText(path, yaml);
        _tempFiles.Add(path);
        return ConfigLoader.Load(path);
    }

    private AppConfig LoadSiteFragment(string siteFragment)
    {
        var indentedFragment = string.Join(
            Environment.NewLine,
            siteFragment.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n').Select(line => $"  {line}"));
        var yaml = $$"""
            site:
              name: analytics-test
              title: Analytics Test
            {{indentedFragment}}
            content:
              sources:
                - type: markdown
                  markdown:
                    dir: content
            """;
        var path = Path.Combine(Path.GetTempPath(), $"bukit-analytics-contract-{Guid.NewGuid():N}.yaml");
        File.WriteAllText(path, yaml);
        _tempFiles.Add(path);
        return ConfigLoader.Load(path);
    }

    private static AppConfig WithProvider(AnalyticsProviderConfig provider) => WithProviders(provider);

    private static AppConfig WithProviders(params AnalyticsProviderConfig[] providers) => new()
    {
        Site = new SiteConfig
        {
            Name = "analytics-test",
            Title = "Analytics Test",
            Analytics = new AnalyticsConfig
            {
                Consent = providers.Any(provider =>
                    provider.Type is "google-analytics" or "google-tag-manager")
                    ? CreateDeniedGoogleConsent()
                    : null,
                Providers = providers
            }
        },
        Content = ContentConfigFactory.FromSources(
            [new ContentSourceConfig { Type = "markdown", Markdown = new MarkdownConfig() }])
    };

    private static AnalyticsConsentConfig CreateDeniedGoogleConsent() => new()
    {
        Google = new AnalyticsGoogleConsentConfig
        {
            Mode = "advanced",
            Defaults = new AnalyticsGoogleConsentDefaultsConfig
            {
                AdStorage = "denied",
                AnalyticsStorage = "denied",
                AdUserData = "denied",
                AdPersonalization = "denied"
            }
        }
    };
}
