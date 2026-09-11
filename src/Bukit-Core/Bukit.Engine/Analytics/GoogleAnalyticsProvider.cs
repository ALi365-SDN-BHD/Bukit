namespace Bukit.Engine.Analytics;

internal sealed class GoogleAnalyticsProvider : IAnalyticsProvider
{
    public string Type => "google-analytics";

    public AnalyticsHtmlFragments Render(
        ResolvedAnalyticsProvider provider,
        AnalyticsRenderContext context)
    {
        var measurementId = provider.Options["measurementId"];
        var scriptUrl = AnalyticsValueEncoder.HtmlAttribute(
            $"https://www.googletagmanager.com/gtag/js?id={measurementId}");
        var javascriptMeasurementId = AnalyticsValueEncoder.JavaScriptString(measurementId);
        var pageTitleAttribute = PageTitleAttribute(context);
        var headStart = $$"""
            <script async src="{{scriptUrl}}"></script>
            <script{{pageTitleAttribute}}>
            window.dataLayer = window.dataLayer || [];
            function gtag(){dataLayer.push(arguments);}
            gtag('js', new Date());
            (function(s){var t=s.getAttribute('data-bukit-page-title');gtag('config', '{{javascriptMeasurementId}}', t===null?{}:{'page_title':t});})(document.currentScript);
            </script>
            """;

        return new AnalyticsHtmlFragments(provider.Key, HeadStart: headStart);
    }

    internal AnalyticsHtmlFragments RenderDestination(
        ResolvedAnalyticsProvider provider,
        AnalyticsRenderContext context)
    {
        var measurementId = AnalyticsValueEncoder.JavaScriptString(provider.Options["measurementId"]);
        var pageTitleAttribute = PageTitleAttribute(context);
        var headStart = $$"""
            <script{{pageTitleAttribute}}>
            (function(s){var t=s.getAttribute('data-bukit-page-title');gtag('config', '{{measurementId}}', t===null?{}:{'page_title':t});})(document.currentScript);
            </script>
            """;

        return new AnalyticsHtmlFragments(provider.Key, HeadStart: headStart);
    }

    internal AnalyticsHtmlFragments RenderAfterConsent(
        ResolvedAnalyticsProvider provider,
        AnalyticsRenderContext context)
    {
        var measurementId = provider.Options["measurementId"];
        var scriptUrl = AnalyticsValueEncoder.HtmlAttribute(
            $"https://www.googletagmanager.com/gtag/js?id={measurementId}");
        var javascriptMeasurementId = AnalyticsValueEncoder.JavaScriptString(measurementId);
        var pageTitleAttribute = PageTitleAttribute(context);
        var headStart = $$"""
            <script async src="{{scriptUrl}}"></script>
            <script{{pageTitleAttribute}}>
            gtag('js', new Date());
            (function(s){var t=s.getAttribute('data-bukit-page-title');gtag('config', '{{javascriptMeasurementId}}', t===null?{}:{'page_title':t});})(document.currentScript);
            </script>
            """;

        return new AnalyticsHtmlFragments(provider.Key, HeadStart: headStart);
    }

    private static string PageTitleAttribute(AnalyticsRenderContext context)
        => context.PageTitle is null
            ? string.Empty
            : $" data-bukit-page-title=\"{AnalyticsValueEncoder.HtmlAttribute(context.PageTitle)}\"";
}
