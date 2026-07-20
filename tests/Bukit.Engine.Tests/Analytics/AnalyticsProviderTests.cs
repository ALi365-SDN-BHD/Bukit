using Bukit.Config;
using Bukit.Engine.Analytics;
using Bukit.Shared;
using Xunit;

namespace Bukit.Engine.Tests.Analytics;

public sealed class AnalyticsProviderTests
{
    private static readonly AnalyticsRenderContext FakeContext = new(
        "/never-read/route/",
        "/never-read/output/index.html",
        IsListPage: false,
        BuildExecutionMode.Production);

    [Fact]
    public void GoogleAnalytics_Render_ReturnsExactGoldenFragment()
    {
        var fragments = new GoogleAnalyticsProvider().Render(
            Provider("google-analytics", "google-analytics:G-ABC123", ("measurementId", "G-ABC123")),
            FakeContext);

        Assert.Equal("google-analytics:G-ABC123", fragments.ProviderKey);
        Assert.Equal(
            """
            <script async src="https://www.googletagmanager.com/gtag/js?id=G-ABC123"></script>
            <script>
            window.dataLayer = window.dataLayer || [];
            function gtag(){dataLayer.push(arguments);}
            gtag('js', new Date());
            gtag('config', 'G-ABC123');
            </script>
            """,
            fragments.HeadStart);
        Assert.Null(fragments.HeadEnd);
        Assert.Null(fragments.BodyStart);
    }

    [Fact]
    public void GoogleTagManager_Render_ReturnsExactGoldenFragments()
    {
        var fragments = new GoogleTagManagerProvider().Render(
            Provider("google-tag-manager", "google-tag-manager:GTM-ABC123", ("containerId", "GTM-ABC123")),
            FakeContext);

        Assert.Equal("google-tag-manager:GTM-ABC123", fragments.ProviderKey);
        Assert.Equal(
            "<script>(function(w,d,s,l,i){w[l]=w[l]||[];w[l].push({'gtm.start':new Date().getTime(),event:'gtm.js'});var f=d.getElementsByTagName(s)[0],j=d.createElement(s),dl=l!='dataLayer'?'&l='+l:'';j.async=true;j.src='https://www.googletagmanager.com/gtm.js?id='+i+dl;f.parentNode.insertBefore(j,f);})(window,document,'script','dataLayer','GTM-ABC123');</script>",
            fragments.HeadStart);
        Assert.Null(fragments.HeadEnd);
        Assert.Equal(
            "<noscript><iframe src=\"https://www.googletagmanager.com/ns.html?id=GTM-ABC123\" height=\"0\" width=\"0\" style=\"display:none;visibility:hidden\"></iframe></noscript>",
            fragments.BodyStart);
    }

    [Fact]
    public void Plausible_Render_ReturnsExactGoldenFragment()
    {
        var fragments = new PlausibleProvider().Render(
            Provider(
                "plausible",
                "plausible:example.com",
                ("domain", "example.com"),
                ("scriptUrl", "https://plausible.io/js/script.js")),
            FakeContext);

        Assert.Equal("plausible:example.com", fragments.ProviderKey);
        Assert.Equal(
            "<script defer data-domain=\"example.com\" src=\"https://plausible.io/js/script.js\"></script>",
            fragments.HeadEnd);
        Assert.Null(fragments.HeadStart);
        Assert.Null(fragments.BodyStart);
    }

    [Fact]
    public void Umami_Render_ReturnsExactGoldenFragment()
    {
        var fragments = new UmamiProvider().Render(
            Provider(
                "umami",
                "umami:00000000-0000-0000-0000-000000000000",
                ("websiteId", "00000000-0000-0000-0000-000000000000"),
                ("scriptUrl", "https://analytics.example.com/script.js")),
            FakeContext);

        Assert.Equal("umami:00000000-0000-0000-0000-000000000000", fragments.ProviderKey);
        Assert.Equal(
            "<script defer src=\"https://analytics.example.com/script.js\" data-website-id=\"00000000-0000-0000-0000-000000000000\"></script>",
            fragments.HeadEnd);
        Assert.Null(fragments.HeadStart);
        Assert.Null(fragments.BodyStart);
    }

    [Fact]
    public void GoogleAnalytics_Render_UsesDedicatedJavaScriptAndHtmlAttributeEncoding()
    {
        const string unsafeId = "G-X'</script>&\"";
        var fragments = new GoogleAnalyticsProvider().Render(
            Provider("google-analytics", $"google-analytics:{unsafeId}", ("measurementId", unsafeId)),
            FakeContext);

        Assert.Equal(
            """
            <script async src="https://www.googletagmanager.com/gtag/js?id=G-X&#39;&lt;/script&gt;&amp;&quot;"></script>
            <script>
            window.dataLayer = window.dataLayer || [];
            function gtag(){dataLayer.push(arguments);}
            gtag('js', new Date());
            gtag('config', 'G-X\u0027\u003C/script\u003E\u0026\u0022');
            </script>
            """,
            fragments.HeadStart);
        Assert.Null(fragments.HeadEnd);
    }

    [Fact]
    public void AttributeProviders_HtmlEncodeEveryConfiguredValue()
    {
        var fragments = new PlausibleProvider().Render(
            Provider(
                "plausible",
                "plausible:unsafe",
                ("domain", "a\"&<b.example"),
                ("scriptUrl", "https://example.com/script.js?x=\"&y=<")),
            FakeContext);

        Assert.Equal(
            "<script defer data-domain=\"a&quot;&amp;&lt;b.example\" src=\"https://example.com/script.js?x=&quot;&amp;y=&lt;\"></script>",
            fragments.HeadEnd);
        Assert.Null(fragments.HeadStart);
    }

    [Fact]
    public void CreateDefault_UsesStaticAotSafeRegistrationForExactlyFourProviders()
    {
        var registry = AnalyticsProviderRegistry.CreateDefault();

        Assert.IsType<GoogleAnalyticsProvider>(registry.GetRequired("google-analytics"));
        Assert.IsType<GoogleTagManagerProvider>(registry.GetRequired("google-tag-manager"));
        Assert.IsType<PlausibleProvider>(registry.GetRequired("plausible"));
        Assert.IsType<UmamiProvider>(registry.GetRequired("umami"));
    }

    [Fact]
    public void GetRequired_UnknownType_ThrowsConfigException()
    {
        var registry = AnalyticsProviderRegistry.CreateDefault();

        var exception = Assert.Throws<ConfigException>(() => registry.GetRequired("custom-script"));

        Assert.Contains("custom-script", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Providers_RequireNoIoDependenciesAndRenderWithFakePaths()
    {
        (IAnalyticsProvider Provider, ResolvedAnalyticsProvider Config)[] cases =
        [
            (new GoogleAnalyticsProvider(),
                Provider("google-analytics", "google-analytics:G-ABC123", ("measurementId", "G-ABC123"))),
            (new GoogleTagManagerProvider(),
                Provider("google-tag-manager", "google-tag-manager:GTM-ABC123", ("containerId", "GTM-ABC123"))),
            (new PlausibleProvider(),
                Provider(
                    "plausible",
                    "plausible:example.com",
                    ("domain", "example.com"),
                    ("scriptUrl", "https://plausible.io/js/script.js"))),
            (new UmamiProvider(),
                Provider(
                    "umami",
                    "umami:00000000-0000-0000-0000-000000000000",
                    ("websiteId", "00000000-0000-0000-0000-000000000000"),
                    ("scriptUrl", "https://analytics.example.com/script.js")))
        ];

        foreach (var (provider, config) in cases)
        {
            var fragments = provider.Render(config, FakeContext);

            Assert.True(fragments.HeadStart is not null || fragments.HeadEnd is not null);
        }
    }

    private static ResolvedAnalyticsProvider Provider(
        string type,
        string key,
        params (string Name, string Value)[] options)
        => new()
        {
            Type = type,
            Key = key,
            Options = options.ToDictionary(option => option.Name, option => option.Value, StringComparer.Ordinal)
        };
}
