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
        var headEnd = $$"""
            <script async src="{{scriptUrl}}"></script>
            <script>
            window.dataLayer = window.dataLayer || [];
            function gtag(){dataLayer.push(arguments);}
            gtag('js', new Date());
            gtag('config', '{{javascriptMeasurementId}}');
            </script>
            """;

        return new AnalyticsHtmlFragments(provider.Key, HeadEnd: headEnd);
    }
}
