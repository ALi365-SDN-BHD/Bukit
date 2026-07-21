using Bukit.Config;
using Bukit.Engine.Analytics;
using Bukit.Shared;
using Xunit;

namespace Bukit.Engine.Tests.Analytics;

public sealed class AnalyticsHtmlTransformTests
{
    private static readonly ILogger Logger = new ConsoleLogger(LogLevel.Error);

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Transform_IsIndependentlyCallable_ForEveryHtmlDocumentKind(int documentKindValue)
    {
        var documentKind = (HtmlDocumentKind)documentKindValue;
        var transform = CreateTransform(
            Provider("google-analytics", measurementId: "G-CONTEXT"));
        var context = Context(documentKind);

        var result = transform.Transform(context, "<html><head></head><body></body></html>");

        Assert.Contains("bukit:analytics:google-analytics:G-CONTEXT:head:start", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Transform_RespectsFeatureSwitchAndExecutionModePolicy()
    {
        const string html = "<html><head></head><body></body></html>";
        var disabled = CreateTransform(
            [Provider("google-analytics", measurementId: "G-OFF")],
            enabled: false);
        var productionOnly = CreateTransform(
            Provider("google-analytics", measurementId: "G-PROD"));
        var developmentEnabled = CreateTransform(
            [Provider("google-analytics", measurementId: "G-DEV")],
            productionOnly: false);

        Assert.Equal(html, disabled.Transform(Context(), html));
        Assert.Equal(html, productionOnly.Transform(Context(executionMode: BuildExecutionMode.Development), html));
        Assert.Contains("G-DEV", developmentEnabled.Transform(Context(executionMode: BuildExecutionMode.Development), html), StringComparison.Ordinal);
        Assert.Contains("G-PROD", productionOnly.Transform(Context(), html), StringComparison.Ordinal);
    }

    [Fact]
    public void Transform_RendersFourProviders_InConfigurationOrderAndCorrectLocations()
    {
        var transform = CreateTransform(
            Provider("google-analytics", measurementId: "G-ONE"),
            Provider("google-tag-manager", containerId: "GTM-TWO"),
            Provider("plausible", domain: "example.com", scriptUrl: "https://plausible.io/js/script.js"),
            Provider("umami", websiteId: "00000000-0000-0000-0000-000000000004", scriptUrl: "https://analytics.example.com/script.js"));
        const string html = "<html><HeAd data-x=\"a>b\"><title>x</title></HeAd><BoDy class='page' data-x=\"a>b\"><main>x</main></BoDy></html>";

        var result = transform.Transform(Context(), html);

        Assert.True(HtmlHeadScanner.TryFindHead(result, out var head));
        var ga = result.IndexOf("<!-- bukit:analytics:google-analytics:G-ONE:head:start", StringComparison.Ordinal);
        var gtmHead = result.IndexOf("<!-- bukit:analytics:google-tag-manager:GTM-TWO:head:start", StringComparison.Ordinal);
        var title = result.IndexOf("<title>", StringComparison.Ordinal);
        var plausible = result.IndexOf("bukit:analytics:plausible:example.com:head:start", StringComparison.Ordinal);
        var umami = result.IndexOf("bukit:analytics:umami:00000000-0000-0000-0000-000000000004:head:start", StringComparison.Ordinal);
        var headClose = result.IndexOf("</head>", StringComparison.OrdinalIgnoreCase);
        var bodyStart = HtmlHeadScanner.FindStartTag(result, "body", 0, result.Length);
        var bodyTagEnd = HtmlHeadScanner.FindTagEnd(result, bodyStart) + 1;
        var gtmBody = result.IndexOf("<!-- bukit:analytics:google-tag-manager:GTM-TWO:body:start", StringComparison.Ordinal);

        Assert.Equal(head.ContentStart, ga);
        Assert.True(ga < gtmHead && gtmHead < title && title < plausible && plausible < umami && umami < headClose);
        Assert.Equal(bodyTagEnd, gtmBody);
        Assert.Contains("<!-- bukit:analytics:google-analytics:G-ONE:head:start -->\n", result, StringComparison.Ordinal);
        Assert.Contains("\n<!-- bukit:analytics:google-analytics:G-ONE:head:end -->", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Transform_MultipleGoogleAnalyticsProviders_ShareBootstrapAndKeepConfigOrder()
    {
        var transform = CreateTransform(
            Provider("google-analytics", measurementId: "G-FIRST"),
            Provider("google-tag-manager", containerId: "GTM-MIDDLE"),
            Provider("google-analytics", measurementId: "G-SECOND"),
            Provider("google-analytics", measurementId: "G-THIRD"));
        const string html = "<html><head><title>x</title></head><body></body></html>";

        var first = transform.Transform(Context(), html);
        var second = transform.Transform(Context(), first);
        var third = transform.Transform(Context(), second);

        Assert.Equal(1, Count(third, "googletagmanager.com/gtag/js?id="));
        Assert.Equal(1, Count(third, "window.dataLayer = window.dataLayer || []"));
        Assert.Equal(1, Count(third, "function gtag()"));
        Assert.Equal(1, Count(third, "gtag('js', new Date())"));
        Assert.Equal(3, Count(third, "gtag('config'"));
        Assert.Contains("gtag/js?id=G-FIRST", third, StringComparison.Ordinal);
        Assert.DoesNotContain("gtag/js?id=G-SECOND", third, StringComparison.Ordinal);
        Assert.DoesNotContain("gtag/js?id=G-THIRD", third, StringComparison.Ordinal);
        Assert.Equal(1, Count(third, "bukit:analytics:google-analytics:G-FIRST:head:start"));
        Assert.Equal(1, Count(third, "bukit:analytics:google-analytics:G-SECOND:head:start"));
        Assert.Equal(1, Count(third, "bukit:analytics:google-analytics:G-THIRD:head:start"));

        var firstConfig = third.IndexOf("gtag('config', 'G-FIRST')", StringComparison.Ordinal);
        var secondConfig = third.IndexOf("gtag('config', 'G-SECOND')", StringComparison.Ordinal);
        var thirdConfig = third.IndexOf("gtag('config', 'G-THIRD')", StringComparison.Ordinal);
        var gtm = third.IndexOf("google-tag-manager:GTM-MIDDLE:head:start", StringComparison.Ordinal);
        Assert.True(firstConfig >= 0 && firstConfig < gtm && gtm < secondConfig && secondConfig < thirdConfig);
        Assert.Equal(first, second);
        Assert.Equal(second, third);
    }

    [Fact]
    public void Transform_GoogleConsentDefaultPrecedesEveryGoogleBootstrapAndIsIdempotent()
    {
        var transform = CreateTransform(
            [
                Provider("google-tag-manager", containerId: "GTM-CONSENT"),
                Provider("google-analytics", measurementId: "G-CONSENT-ONE"),
                Provider("google-analytics", measurementId: "G-CONSENT-TWO")
            ],
            googleConsent: GoogleConsent(waitForUpdateMs: 500));
        const string html = "<html><head><title>x</title></head><body></body></html>";

        var first = transform.Transform(Context(), html);
        var second = transform.Transform(Context(), first);
        var third = transform.Transform(Context(), second);

        var consent = third.IndexOf("bukit:analytics:google-consent:default:head:start", StringComparison.Ordinal);
        var gtm = third.IndexOf("bukit:analytics:google-tag-manager:GTM-CONSENT:head:start", StringComparison.Ordinal);
        var ga = third.IndexOf("bukit:analytics:google-analytics:G-CONSENT-ONE:head:start", StringComparison.Ordinal);
        Assert.True(consent >= 0 && consent < gtm && consent < ga);
        Assert.Equal(1, Count(third, "bukit:analytics:google-consent:default:head:start"));
        Assert.Equal(1, Count(third, "window.dataLayer = window.dataLayer || []"));
        Assert.Equal(1, Count(third, "function gtag(){dataLayer.push(arguments);}"));
        Assert.Equal(1, Count(third, "gtag('consent', 'default'"));
        Assert.Contains("'ad_storage': 'denied'", third, StringComparison.Ordinal);
        Assert.Contains("'analytics_storage': 'denied'", third, StringComparison.Ordinal);
        Assert.Contains("'ad_user_data': 'denied'", third, StringComparison.Ordinal);
        Assert.Contains("'ad_personalization': 'denied'", third, StringComparison.Ordinal);
        Assert.Contains("'wait_for_update': 500", third, StringComparison.Ordinal);
        Assert.DoesNotContain("gtag('consent', 'update'", third, StringComparison.Ordinal);
        Assert.Equal(first, second);
        Assert.Equal(second, third);
    }

    [Fact]
    public void Transform_RemovesGoogleConsentBlockWhenGoogleProvidersAreRemoved()
    {
        var original = CreateTransform(
            [Provider("google-analytics", measurementId: "G-REMOVE-CONSENT")],
            googleConsent: GoogleConsent());
        var updated = CreateTransform(Array.Empty<AnalyticsProviderConfig>());
        const string html = "<html><head></head><body></body></html>";

        var beforeRemoval = original.Transform(Context(), html);
        var afterRemoval = updated.Transform(Context(), beforeRemoval);

        Assert.Contains("bukit:analytics:google-consent:default:head:start", beforeRemoval, StringComparison.Ordinal);
        Assert.DoesNotContain("bukit:analytics:google-consent", afterRemoval, StringComparison.Ordinal);
        Assert.Equal(html, afterRemoval);
    }

    [Fact]
    public void Transform_ProductionOnlyDevelopmentPassRemovesGoogleConsentAndProviderBlocks()
    {
        var transform = CreateTransform(
            [Provider("google-tag-manager", containerId: "GTM-DEV-CLEAN")],
            googleConsent: GoogleConsent());
        const string html = "<html><head></head><body></body></html>";

        var production = transform.Transform(Context(), html);
        var development = transform.Transform(
            Context(executionMode: BuildExecutionMode.Development),
            production);

        Assert.Contains("bukit:analytics:google-consent:default:head:start", production, StringComparison.Ordinal);
        Assert.Equal(html, development);
    }

    [Fact]
    public void Transform_RemovesConfigOnlyBlockWhenGoogleAnalyticsDestinationIsRemoved()
    {
        var original = CreateTransform(
            Provider("google-analytics", measurementId: "G-KEEP"),
            Provider("google-analytics", measurementId: "G-REMOVE"));
        var updated = CreateTransform(Provider("google-analytics", measurementId: "G-KEEP"));
        const string html = "<html><head></head><body></body></html>";

        var beforeRemoval = original.Transform(Context(), html);
        var afterRemoval = updated.Transform(Context(), beforeRemoval);

        Assert.Equal(1, Count(beforeRemoval, "googletagmanager.com/gtag/js?id="));
        Assert.Equal(2, Count(beforeRemoval, "gtag('config'"));
        Assert.DoesNotContain("G-REMOVE", afterRemoval, StringComparison.Ordinal);
        Assert.Equal(1, Count(afterRemoval, "googletagmanager.com/gtag/js?id="));
        Assert.Equal(1, Count(afterRemoval, "gtag('config'"));
    }

    [Fact]
    public void Transform_MigratesPlausibleLegacyBlockToSiteSpecificAndRemainsIdempotent()
    {
        var legacy = CreateTransform(Provider(
            "plausible",
            domain: "example.com",
            snippetMode: "legacy",
            scriptUrl: "https://plausible.io/js/script.js"));
        var siteSpecific = CreateTransform(Provider(
            "plausible",
            domain: "example.com",
            snippetMode: "site-specific",
            scriptUrl: "https://plausible.io/js/pa-AN07TEST.js"));
        const string html = "<html><head></head><body></body></html>";

        var legacyHtml = legacy.Transform(Context(), html);
        var upgraded = siteSpecific.Transform(Context(), legacyHtml);
        var repeated = siteSpecific.Transform(Context(), upgraded);

        Assert.DoesNotContain("data-domain", upgraded, StringComparison.Ordinal);
        Assert.DoesNotContain("/js/script.js", upgraded, StringComparison.Ordinal);
        Assert.Contains("<script async src=\"https://plausible.io/js/pa-AN07TEST.js\"></script>", upgraded, StringComparison.Ordinal);
        Assert.Contains("plausible.init()", upgraded, StringComparison.Ordinal);
        Assert.Equal(1, Count(upgraded, "bukit:analytics:plausible:example.com:head:start"));
        Assert.Equal(upgraded, repeated);
    }

    [Fact]
    public void Transform_SkipsOnlyTheLocationWhoseContainerIsMissing()
    {
        var transform = CreateTransform(Provider("google-tag-manager", containerId: "GTM-ONLY"));

        var withoutHead = transform.Transform(Context(), "<html><body><main>x</main></body></html>");
        var withoutBody = transform.Transform(Context(), "<html><head></head><main>x</main></html>");

        Assert.DoesNotContain(":head:start", withoutHead, StringComparison.Ordinal);
        Assert.Contains(":body:start", withoutHead, StringComparison.Ordinal);
        Assert.Contains(":head:start", withoutBody, StringComparison.Ordinal);
        Assert.DoesNotContain(":body:start", withoutBody, StringComparison.Ordinal);
    }

    [Fact]
    public void Transform_IsIdempotent_AndRemovesManagedBlocksForRemovedProviders()
    {
        var original = CreateTransform(
            Provider("google-analytics", measurementId: "G-KEEP"),
            Provider("plausible", domain: "remove.example", scriptUrl: "https://plausible.io/js/script.js"));
        var updated = CreateTransform(Provider("google-analytics", measurementId: "G-KEEP"));
        const string html = "<html><head></head><body></body></html>";

        var first = original.Transform(Context(), html);
        var second = original.Transform(Context(), first);
        var afterRemoval = updated.Transform(Context(), second);

        Assert.Equal(first, second);
        Assert.DoesNotContain("plausible:remove.example", afterRemoval, StringComparison.Ordinal);
        Assert.Equal(1, Count(afterRemoval, "bukit:analytics:google-analytics:G-KEEP:head:start"));
    }

    [Fact]
    public void Transform_MigratesLegacyGoogleHeadEndBlockToHeadStartAndRemainsIdempotent()
    {
        var transform = CreateTransform(Provider("google-analytics", measurementId: "G-MOVE"));
        const string html = """
            <html><head><title>x</title><!-- bukit:analytics:google-analytics:G-MOVE:head:start -->
            <script>legacyHeadEnd()</script>
            <!-- bukit:analytics:google-analytics:G-MOVE:head:end --></head><body></body></html>
            """;

        var first = transform.Transform(Context(), html);
        var second = transform.Transform(Context(), first);
        var third = transform.Transform(Context(), second);

        Assert.True(HtmlHeadScanner.TryFindHead(first, out var head));
        Assert.Equal(
            head.ContentStart,
            first.IndexOf("<!-- bukit:analytics:google-analytics:G-MOVE:head:start", StringComparison.Ordinal));
        Assert.DoesNotContain("legacyHeadEnd", first, StringComparison.Ordinal);
        Assert.Equal(1, Count(first, "bukit:analytics:google-analytics:G-MOVE:head:start"));
        Assert.Equal(first, second);
        Assert.Equal(second, third);
    }

    [Fact]
    public void Transform_InjectsHeadStartIntoOnlyTheFirstHeadElement()
    {
        var transform = CreateTransform(Provider("google-tag-manager", containerId: "GTM-FIRST"));
        const string html = "<html><HEAD><title>first</title></HEAD><head><title>second</title></head><body></body></html>";

        var result = transform.Transform(Context(), html);

        var firstTitle = result.IndexOf("<title>first", StringComparison.Ordinal);
        var secondHead = result.IndexOf("<head>", firstTitle, StringComparison.Ordinal);
        var marker = result.IndexOf("<!-- bukit:analytics:google-tag-manager:GTM-FIRST:head:start", StringComparison.Ordinal);
        Assert.True(marker >= 0 && marker < firstTitle && firstTitle < secondHead);
        Assert.Equal(1, Count(result, "bukit:analytics:google-tag-manager:GTM-FIRST:head:start"));
    }

    [Fact]
    public void Transform_GroupsProvidersBySlotWhilePreservingOrderWithinEachSlot()
    {
        var transform = CreateTransform(
            Provider("plausible", domain: "first.example", scriptUrl: "https://first.example/script.js"),
            Provider("google-tag-manager", containerId: "GTM-SECOND"),
            Provider("umami", websiteId: "00000000-0000-0000-0000-000000000003", scriptUrl: "https://third.example/script.js"),
            Provider("google-analytics", measurementId: "G-FOURTH"));
        const string html = "<html><head><meta name='theme'></head><body></body></html>";

        var result = transform.Transform(Context(), html);

        var gtm = result.IndexOf("google-tag-manager:GTM-SECOND:head:start", StringComparison.Ordinal);
        var ga = result.IndexOf("google-analytics:G-FOURTH:head:start", StringComparison.Ordinal);
        var theme = result.IndexOf("<meta name='theme'>", StringComparison.Ordinal);
        var plausible = result.IndexOf("plausible:first.example:head:start", StringComparison.Ordinal);
        var umami = result.IndexOf("umami:00000000-0000-0000-0000-000000000003:head:start", StringComparison.Ordinal);
        Assert.True(gtm >= 0 && gtm < ga && ga < theme && theme < plausible && plausible < umami);
    }

    [Fact]
    public void Transform_RemainsIdempotent_WhenUnpairedStartPrecedesManagedBlock()
    {
        var transform = CreateTransform(Provider("google-analytics", measurementId: "G-ACTIVE"));
        const string html = """
            <html><head>
            <!-- bukit:analytics:google-analytics:G-ORPHAN:head:start -->
            </head><body></body></html>
            """;

        var first = transform.Transform(Context(), html);
        var second = transform.Transform(Context(), first);
        var third = transform.Transform(Context(), second);

        Assert.Equal(first, second);
        Assert.Equal(second, third);
        Assert.Equal(1, Count(third, "bukit:analytics:google-analytics:G-ACTIVE:head:start"));
        Assert.Contains("bukit:analytics:google-analytics:G-ORPHAN:head:start", third, StringComparison.Ordinal);
    }

    [Fact]
    public void Transform_PreservesUnmarkedScriptsAndMalformedOrUnpairedManagedComments()
    {
        var transform = CreateTransform(Array.Empty<AnalyticsProviderConfig>());
        const string html = """
            <html><head>
            <script src="https://www.googletagmanager.com/gtag/js?id=G-USER"></script>
            <!-- bukit:analytics:google-analytics:G-BROKEN:head:start -->
            <script>userOwned()</script>
            <!-- bukit:analytics:google-analytics:G-OTHER:head:end -->
            <!-- bukit:analytics:plausible:unpaired.example:head:start -->
            </head><body></body></html>
            """;

        var result = transform.Transform(Context(), html);

        Assert.Equal(html, result);
    }

    [Fact]
    public void Transform_DoesNotTreatManagedMarkerTextInsideRawTextOrAttributesAsHtmlComments()
    {
        var transform = CreateTransform(Array.Empty<AnalyticsProviderConfig>());
        const string html = """
            <html><head>
            <script>const marker = "<!-- bukit:analytics:google-analytics:G-USER:head:start -->x<!-- bukit:analytics:google-analytics:G-USER:head:end -->";</script>
            <style>/* <!-- bukit:analytics:plausible:user.example:head:start -->x<!-- bukit:analytics:plausible:user.example:head:end --> */</style>
            <title><!-- bukit:analytics:google-analytics:G-TITLE:head:start -->x<!-- bukit:analytics:google-analytics:G-TITLE:head:end --></title>
            <meta content="<!-- bukit:analytics:umami:00000000-0000-0000-0000-000000000001:head:start -->x<!-- bukit:analytics:umami:00000000-0000-0000-0000-000000000001:head:end -->">
            </head><body><textarea><!-- bukit:analytics:google-tag-manager:GTM-TEXT:body:start -->x<!-- bukit:analytics:google-tag-manager:GTM-TEXT:body:end --></textarea></body></html>
            """;

        var result = transform.Transform(Context(), html);

        Assert.Equal(html, result);
    }

    [Fact]
    public void Transform_PreservesEntireNestedOrCrossedManagedMarkerGroups()
    {
        var transform = CreateTransform(Array.Empty<AnalyticsProviderConfig>());
        const string nested = """
            <html><head>
            <!-- bukit:analytics:google-analytics:G-OUTER:head:start -->
            outer-before
            <!-- bukit:analytics:plausible:nested.example:head:start -->
            user-content
            <!-- bukit:analytics:plausible:nested.example:head:end -->
            outer-after
            <!-- bukit:analytics:google-analytics:G-OUTER:head:end -->
            </head><body></body></html>
            """;
        const string crossed = """
            <html><head>
            <!-- bukit:analytics:google-analytics:G-A:head:start -->
            <!-- bukit:analytics:plausible:b.example:head:start -->
            user-content
            <!-- bukit:analytics:google-analytics:G-A:head:end -->
            <!-- bukit:analytics:plausible:b.example:head:end -->
            </head><body></body></html>
            """;
        const string boundedMismatch = """
            <html><head>
            <!-- bukit:analytics:google-analytics:G-OUTER:head:start -->
            <!-- bukit:analytics:plausible:nested.example:head:start -->
            user-content
            <!-- bukit:analytics:plausible:nested.example:head:end -->
            <!-- bukit:analytics:google-analytics:G-DIFFERENT:head:end -->
            </head><body></body></html>
            """;

        Assert.Equal(nested, transform.Transform(Context(), nested));
        Assert.Equal(crossed, transform.Transform(Context(), crossed));
        Assert.Equal(boundedMismatch, transform.Transform(Context(), boundedMismatch));
    }

    [Fact]
    public void Transform_UpdatesProcessedInjectedAndMissingLocationCounters()
    {
        var resolved = AnalyticsConfigNormalizer.Normalize(new AnalyticsConfig
        {
            Providers = [Provider("google-tag-manager", containerId: "GTM-STATE")]
        });
        var state = new AnalyticsBuildState(true, resolved, BuildExecutionMode.Production);
        var transform = new AnalyticsHtmlTransform(
            resolved,
            AnalyticsProviderRegistry.CreateDefault(),
            state);

        transform.Transform(Context(), "<html><head></head><main>x</main></html>");
        transform.Transform(Context(), "<html><body></body></html>");

        var snapshot = state.Snapshot();
        Assert.Equal(2, snapshot.ProcessedHtml);
        Assert.Equal(2, snapshot.InjectedHtml);
        Assert.Equal(1, snapshot.SkippedByReason["head_missing"]);
        Assert.Equal(1, snapshot.SkippedByReason["body_missing"]);
    }

    [Fact]
    public void Transform_WhenProviderFails_RecordsTransformFailed()
    {
        var resolved = new ResolvedAnalyticsConfig
        {
            Enabled = true,
            ProductionOnly = false,
            Providers =
            [
                new ResolvedAnalyticsProvider
                {
                    Type = "not-registered",
                    Key = "not-registered:test"
                }
            ]
        };
        var state = new AnalyticsBuildState(true, resolved, BuildExecutionMode.Production);
        var transform = new AnalyticsHtmlTransform(
            resolved,
            AnalyticsProviderRegistry.CreateDefault(),
            state);

        Assert.Throws<ConfigException>(() =>
        {
            transform.Transform(Context(), "<html><head></head><body></body></html>");
        });
        Assert.Equal(1, state.Snapshot().SkippedByReason["transform_failed"]);
    }

    private static AnalyticsHtmlTransform CreateTransform(params AnalyticsProviderConfig[] providers)
        => CreateTransform(providers, enabled: true, productionOnly: true);

    private static AnalyticsHtmlTransform CreateTransform(
        IReadOnlyList<AnalyticsProviderConfig> providers,
        bool enabled = true,
        bool productionOnly = true,
        AnalyticsGoogleConsentConfig? googleConsent = null)
        => new(
            AnalyticsConfigNormalizer.Normalize(new AnalyticsConfig
            {
                Enabled = enabled,
                ProductionOnly = productionOnly,
                Providers = providers,
                Consent = googleConsent is null
                    ? null
                    : new AnalyticsConsentConfig { Google = googleConsent }
            }),
            AnalyticsProviderRegistry.CreateDefault());

    private static AnalyticsGoogleConsentConfig GoogleConsent(int? waitForUpdateMs = null)
        => new()
        {
            Mode = "advanced",
            Defaults = new AnalyticsGoogleConsentDefaultsConfig
            {
                AdStorage = "denied",
                AnalyticsStorage = "denied",
                AdUserData = "denied",
                AdPersonalization = "denied"
            },
            WaitForUpdateMs = waitForUpdateMs
        };

    private static HtmlTransformContext Context(
        HtmlDocumentKind documentKind = HtmlDocumentKind.Content,
        BuildExecutionMode executionMode = BuildExecutionMode.Production)
        => new("/route/", "route/index.html", documentKind, executionMode, Logger);

    private static AnalyticsProviderConfig Provider(
        string type,
        string? measurementId = null,
        string? containerId = null,
        string? domain = null,
        string? snippetMode = null,
        string? websiteId = null,
        string? scriptUrl = null)
        => new()
        {
            Type = type,
            MeasurementId = measurementId,
            ContainerId = containerId,
            Domain = domain,
            SnippetMode = snippetMode ?? (type == "plausible" ? "legacy" : null),
            WebsiteId = websiteId,
            ScriptUrl = scriptUrl
        };

    private static int Count(string value, string needle)
        => (value.Length - value.Replace(needle, string.Empty, StringComparison.Ordinal).Length) / needle.Length;
}
