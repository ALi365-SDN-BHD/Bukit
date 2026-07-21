namespace Bukit.Engine.Analytics;

internal sealed class PlausibleProvider : IAnalyticsProvider
{
    public string Type => "plausible";

    public AnalyticsHtmlFragments Render(
        ResolvedAnalyticsProvider provider,
        AnalyticsRenderContext context)
    {
        var scriptUrl = AnalyticsValueEncoder.HtmlAttribute(provider.Options["scriptUrl"]);
        if (provider.Options["mode"] == "site-specific")
        {
            var headEnd = $$$"""
                <script async src="{{{scriptUrl}}}"></script>
                <script>
                window.plausible=window.plausible||function(){(plausible.q=plausible.q||[]).push(arguments)},plausible.init=plausible.init||function(i){plausible.o=i||{}};
                plausible.init()
                </script>
                """;
            return new AnalyticsHtmlFragments(provider.Key, HeadEnd: headEnd);
        }

        return new AnalyticsHtmlFragments(
            provider.Key,
            HeadEnd: $"<script defer data-domain=\"{AnalyticsValueEncoder.HtmlAttribute(provider.Options["domain"])}\" src=\"{scriptUrl}\"></script>");
    }
}
