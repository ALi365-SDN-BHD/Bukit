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
        var headStart = $$"""
            <script async src="{{scriptUrl}}"></script>
            <script>
            window.dataLayer = window.dataLayer || [];
            function gtag(){dataLayer.push(arguments);}
            gtag('js', new Date());
            gtag('config', '{{javascriptMeasurementId}}');
            </script>
            """;

        return new AnalyticsHtmlFragments(provider.Key, HeadStart: headStart);
    }

    internal AnalyticsHtmlFragments RenderDestination(
        ResolvedAnalyticsProvider provider,
        AnalyticsRenderContext context)
    {
        var measurementId = AnalyticsValueEncoder.JavaScriptString(provider.Options["measurementId"]);
        var headStart = $$"""
            <script>
            gtag('config', '{{measurementId}}');
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
        var headStart = $$"""
            <script async src="{{scriptUrl}}"></script>
            <script>
            gtag('js', new Date());
            gtag('config', '{{javascriptMeasurementId}}');
            </script>
            """;

        return new AnalyticsHtmlFragments(provider.Key, HeadStart: headStart);
    }
}
