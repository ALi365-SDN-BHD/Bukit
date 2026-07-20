namespace Bukit.Engine.Analytics;

internal sealed class GoogleTagManagerProvider : IAnalyticsProvider
{
    public string Type => "google-tag-manager";

    public AnalyticsHtmlFragments Render(
        ResolvedAnalyticsProvider provider,
        AnalyticsRenderContext context)
    {
        var containerId = provider.Options["containerId"];
        var javascriptContainerId = AnalyticsValueEncoder.JavaScriptString(containerId);
        var iframeUrl = AnalyticsValueEncoder.HtmlAttribute(
            $"https://www.googletagmanager.com/ns.html?id={containerId}");

        return new AnalyticsHtmlFragments(
            provider.Key,
            HeadStart: $"<script>(function(w,d,s,l,i){{w[l]=w[l]||[];w[l].push({{'gtm.start':new Date().getTime(),event:'gtm.js'}});var f=d.getElementsByTagName(s)[0],j=d.createElement(s),dl=l!='dataLayer'?'&l='+l:'';j.async=true;j.src='https://www.googletagmanager.com/gtm.js?id='+i+dl;f.parentNode.insertBefore(j,f);}})(window,document,'script','dataLayer','{javascriptContainerId}');</script>",
            BodyStart: $"<noscript><iframe src=\"{iframeUrl}\" height=\"0\" width=\"0\" style=\"display:none;visibility:hidden\"></iframe></noscript>");
    }
}
