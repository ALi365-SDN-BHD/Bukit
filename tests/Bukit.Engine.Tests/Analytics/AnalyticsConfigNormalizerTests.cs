using Bukit.Config;
using Bukit.Engine.Analytics;
using Xunit;

namespace Bukit.Engine.Tests.Analytics;

public sealed class AnalyticsConfigNormalizerTests
{
    [Fact]
    public void Normalize_DefaultConfig_PreservesDefaultsAndHasNoProviders()
    {
        var resolved = AnalyticsConfigNormalizer.Normalize(new AnalyticsConfig());

        Assert.True(resolved.Enabled);
        Assert.True(resolved.ProductionOnly);
        Assert.Empty(resolved.Providers);
    }

    [Fact]
    public void Normalize_PreservesProviderOrderAndGoogleIdentifiersExactly()
    {
        var config = new AnalyticsConfig
        {
            Enabled = false,
            ProductionOnly = false,
            Providers =
            [
                new AnalyticsProviderConfig
                {
                    Type = "google-tag-manager",
                    ContainerId = "GTM-AbC123"
                },
                new AnalyticsProviderConfig
                {
                    Type = "google-analytics",
                    MeasurementId = "G-MiXeD456"
                }
            ]
        };

        var resolved = AnalyticsConfigNormalizer.Normalize(config);

        Assert.False(resolved.Enabled);
        Assert.False(resolved.ProductionOnly);
        Assert.Equal(
            ["google-tag-manager", "google-analytics"],
            resolved.Providers.Select(provider => provider.Type));
        Assert.Equal("GTM-AbC123", resolved.Providers[0].Options["containerId"]);
        Assert.Equal("G-MiXeD456", resolved.Providers[1].Options["measurementId"]);
        Assert.Equal("google-tag-manager:GTM-AbC123", resolved.Providers[0].Key);
        Assert.Equal("google-analytics:G-MiXeD456", resolved.Providers[1].Key);
    }

    [Fact]
    public void Normalize_CanonicalizesIdnAndUuidWhileKeepingValidatedScriptUrls()
    {
        var config = new AnalyticsConfig
        {
            Providers =
            [
                new AnalyticsProviderConfig
                {
                    Type = "plausible",
                    Domain = "BÜCHER.Example",
                    SnippetMode = "legacy",
                    ScriptUrl = "https://stats.example/Custom/Script.js?site=One"
                },
                new AnalyticsProviderConfig
                {
                    Type = "umami",
                    WebsiteId = "89F9C547-2017-4B05-8A56-8F40B488F927",
                    ScriptUrl = "https://analytics.example.com/script.js"
                }
            ]
        };

        var resolved = AnalyticsConfigNormalizer.Normalize(config);

        var plausible = resolved.Providers[0];
        Assert.Equal("xn--bcher-kva.example", plausible.Options["domain"]);
        Assert.Equal("https://stats.example/Custom/Script.js?site=One", plausible.Options["scriptUrl"]);
        Assert.Equal("plausible:xn--bcher-kva.example", plausible.Key);
        Assert.Equal("legacy", plausible.Options["mode"]);

        var umami = resolved.Providers[1];
        Assert.Equal("89f9c547-2017-4b05-8a56-8f40b488f927", umami.Options["websiteId"]);
        Assert.Equal("https://analytics.example.com/script.js", umami.Options["scriptUrl"]);
        Assert.Equal("umami:89f9c547-2017-4b05-8a56-8f40b488f927", umami.Key);
    }

    [Fact]
    public void Normalize_PlausibleSiteSpecificScript_UsesSafeStableKeyWithoutDomain()
    {
        var config = new AnalyticsConfig
        {
            Providers =
            [
                new AnalyticsProviderConfig
                {
                    Type = "plausible",
                    Domain = "example.com",
                    SnippetMode = "site-specific",
                    ScriptUrl = "https://plausible.io/js/pa-AN07TEST.js"
                }
            ]
        };

        var resolved = Assert.Single(AnalyticsConfigNormalizer.Normalize(config).Providers);

        Assert.Equal("plausible:example.com", resolved.Key);
        Assert.Equal("site-specific", resolved.Options["mode"]);
        Assert.Equal("https://plausible.io/js/pa-AN07TEST.js", resolved.Options["scriptUrl"]);
        Assert.Equal("example.com", resolved.Options["domain"]);
    }

    [Fact]
    public void Normalize_GoogleConsentAndCsp_PreservesValidatedPolicyWithoutChangingProviders()
    {
        var config = new AnalyticsConfig
        {
            Consent = new AnalyticsConsentConfig
            {
                Google = new AnalyticsGoogleConsentConfig
                {
                    Mode = "advanced",
                    Defaults = new AnalyticsGoogleConsentDefaultsConfig
                    {
                        AdStorage = "denied",
                        AnalyticsStorage = "granted",
                        AdUserData = "denied",
                        AdPersonalization = "granted"
                    },
                    WaitForUpdateMs = 250
                }
            },
            Csp = new AnalyticsCspConfig { Mode = "requirements-report" },
            Providers =
            [
                new AnalyticsProviderConfig
                {
                    Type = "google-analytics",
                    MeasurementId = "G-CONSENT123"
                }
            ]
        };

        var resolved = AnalyticsConfigNormalizer.Normalize(config);

        Assert.Equal("requirements-report", resolved.CspMode);
        var consent = Assert.IsType<ResolvedGoogleConsent>(resolved.GoogleConsent);
        Assert.Equal("advanced", consent.Mode);
        Assert.Equal("denied", consent.AdStorage);
        Assert.Equal("granted", consent.AnalyticsStorage);
        Assert.Equal("denied", consent.AdUserData);
        Assert.Equal("granted", consent.AdPersonalization);
        Assert.Equal(250, consent.WaitForUpdateMs);
        Assert.Equal("google-analytics:G-CONSENT123", Assert.Single(resolved.Providers).Key);
    }
}
